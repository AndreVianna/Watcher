namespace DotnetToolbox.Net.DataExchange;

internal sealed class DataExchangeAgent : IDataExchangeAgent {
    public const string DefaultLocalAddress = "127.0.0.1";
    public const ushort DefaultRequestPort = 6200;
    public const int DefaultBufferSize = 1024;

    private readonly DataExchangeAgentOptions _options;
    private readonly IRequestHandlers _requestHandlers;
    private readonly ILogger _logger;
    private readonly Agent _agent;

    private bool _isDisposed;
    private CancellationTokenSource _agentControl = default!;

    public DataExchangeAgent(IOptions<DataExchangeAgentOptions> options, IRequestHandlers requestHandlers, ILoggerFactory loggerFactory) {
        _requestHandlers = requestHandlers;
        _options = options.Value;
        _logger = loggerFactory.CreateLogger("Agent");
        _agent = CreateAgent();
    }

    public void Dispose() {
        if (_isDisposed) return;
        Stop();
        _agent.Connections.Clear();
        _isDisposed = true;
    }

    public Connection ToConnection => new() {
        Id = _agent.Id,
        EndPoint = _agent.EndPoint.ToString(),
    };

    public bool IsRunning { get; private set; }

    public string AddConnection(Connection connection) {
        if (_agent.Connections.TryGetValue(connection.Id, out _))
            return "Client is already registered.";
        if (_agent.Connections.Values.Any(i => i.EndPoint.ToString() == connection.EndPoint))
            return "Client endpoint already in use.";
        var agent = new Agent(connection);
        _agent.Connections.Add(connection.Id, agent);
        return "Success";
    }

    public IEnumerable<Connection> GetConnections()
        => _agent.Connections.Values.Select(i => new Connection {
            Id = i.Id,
            EndPoint = i.EndPoint.ToString(),
        });

    public void RemoveConnection(string id)
        => _agent.Connections.Remove(id);

    public void Start() {
        try {
            if (IsRunning) return;
            _logger.LogDebug("Starting agent...");
            ExecuteStart();
            IsRunning = true;
            _logger.LogDebug("Agent started.");
        }
        catch (OperationCanceledException ex) {
            _logger.LogWarning(ex, "Cancellation requested.");
            Stop();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while starting agent.");
            Stop();
        }
    }

    public void Stop() {
        try {
            if (!IsRunning) return;
            _logger.LogDebug("Stopping agent...");
            ExecuteStop();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while stopping agent.");
        }
        finally {
            IsRunning = false;
            _logger.LogDebug("Agent stopped.");
        }
    }

    private void ExecuteStart() {
        _agentControl = new CancellationTokenSource();
        StartListeningToRequests().FireAndForget();
    }

    private void ExecuteStop() {
        _agentControl.Cancel();
        _agentControl = default!;
        StopListeningToRequest();
    }

    public async Task<Response> SendRequestTo(string connectionId, Request request, CancellationToken ct) {
        _logger.LogDebug("Getting connection '{Id}' information...", connectionId);
        var target = _agent.Connections.GetValueOrDefault(connectionId);
        if (target is null) {
            _logger.LogDebug("Connection '{Id}' not found.", connectionId);
            return Response.NotFound();
        }
        var tcpClient = await SendRequest(target.EndPoint, request, ct).ConfigureAwait(false);
        if (tcpClient is null)
            return Response.InternalServerError("Failed to send request.");
        _logger.LogDebug("Waiting for response from {RemoteAddress}.", target.EndPoint);
        var message = await ReadResponse(tcpClient, ct);
        return message.Response;
    }

    private async Task StartListeningToRequests() {
        try {
            _agent.Listener.Start();
            var ct = _agentControl.Token;
            while (!_agentControl.IsCancellationRequested) {
                _logger.LogDebug("Listening for requests at {RequestListenerEndPoint}.", _agent.EndPoint);
                var tcpClient = await _agent.Listener.GetContextAsync();
                if (tcpClient.Request.IsWebSocketRequest) {
                    var websocketContext = await tcpClient.AcceptWebSocketAsync(null);
                    try {
                        _logger.LogDebug("Receiving request from {RemoteAddress}.", tcpClient.Request.RemoteEndPoint);
                        var request = await ReadRequest(websocketContext, ct).ConfigureAwait(false);
                        _logger.LogDebug("Request received.");
                        await RespondRequest(tcpClient, websocketContext, request, ct).ConfigureAwait(false);
                    }
                    finally {

                        await websocketContext.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", ct);
                    }
                }
                else {
                    tcpClient.Response.StatusCode = 400; // Bad Request
                    tcpClient.Response.Close();
                }
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while listening to requests.");
        }
        finally {
            _agent.Listener.Stop();
        }
    }

    private async Task RespondRequest(HttpListenerContext listener, WebSocketContext tcpClient, Request request, CancellationToken ct) {
        var requestControl = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var id = IRequestHandler.CreateId($"{request.Verb} {request.Route}", request.GetType());
        var handler = _requestHandlers.GetValueOrDefault(id);
        var response = handler switch {
            null => Response.NotFound(),
            _ => await handler.Handle(request, requestControl).ConfigureAwait(false),
        };
        var message = new RequestResponse {
            RequestId = request.Id,
            Response = response,
        };
        await SendResponse(listener, tcpClient, message, requestControl.Token).ConfigureAwait(false);
    }

    private async Task SendResponse(HttpListenerContext listener, WebSocketContext tcpClient, RequestResponse message, CancellationToken ct) {
        try {
            _logger.LogDebug("Sending response to {RemoteAddress}.", listener.Request.RemoteEndPoint);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            await tcpClient.WebSocket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Binary, true, ct).ConfigureAwait(false);
            _logger.LogDebug("Response sent to {RemoteAddress}.", listener.Request.RemoteEndPoint);
        }
        catch (OperationCanceledException ex) {
            _logger.LogWarning(ex, "Sending response to {RemoteAddress} was canceled.", listener.Request.RemoteEndPoint);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while responding to {RemoteAddress}.", listener.Request.RemoteEndPoint);
        }
    }

    private async Task<ClientWebSocket?> SendRequest(EndPoint remoteAddress, Request request, CancellationToken ct) {
        var tcpClient = new ClientWebSocket();
        await tcpClient.ConnectAsync(new Uri($"ws://{remoteAddress}"), ct);
        try {
            _logger.LogDebug("Sending request to {RemoteAddress}.", remoteAddress);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
            await tcpClient.SendAsync(bytes.AsMemory(), WebSocketMessageType.Binary, true, ct).ConfigureAwait(false);
            _logger.LogDebug("Request sent to {RemoteAddress}.", remoteAddress);
            return tcpClient;
        }
        catch (OperationCanceledException ex) {
            _logger.LogWarning(ex, "Sending request to {RemoteAddress} was canceled.", remoteAddress);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while sending request to {RemoteAddress}.", remoteAddress);
        }

        return null;
    }

    private Agent CreateAgent() {
        var id = _options.Id ?? Guid.NewGuid().ToString();
        _logger.LogDebug("Creating agent '{Id}'...", id);
        var endPoint = _options.Endpoint ?? $"{DefaultLocalAddress}:{DefaultRequestPort}";
        var agent = new Agent(id, endPoint);
        _logger.LogDebug("Agent '{Id}' created at [{EndPoint}]...", id, endPoint);
        return agent;
    }

    private void StopListeningToRequest()
        => _agent.Listener.Stop();

    private async Task<Request> ReadRequest(WebSocketContext tcpClient, CancellationToken ct) {
        await using var memoryStream = new MemoryStream();
        var buffer = new byte[_options.BufferSize ?? DefaultBufferSize];
        var isEndOfMessage = false;
        while (!isEndOfMessage) {
            var result = await tcpClient.WebSocket.ReceiveAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            await memoryStream.WriteAsync(buffer[..result.Count].AsMemory(), ct);
            isEndOfMessage = result.EndOfMessage;
        }
        var json = Encoding.UTF8.GetString(memoryStream.ToArray());
        var content = JsonSerializer.Deserialize<Request>(json);
        return IsNotNull(content);
    }

    private async Task<RequestResponse> ReadResponse(WebSocket tcpClient, CancellationToken ct) {
        await using var memoryStream = new MemoryStream();
        var isEndOfMessage = false;
        while (!isEndOfMessage) {
            var buffer = new byte[_options.BufferSize ?? DefaultBufferSize];
            var result = await tcpClient.ReceiveAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            await memoryStream.WriteAsync(buffer[..result.Count].AsMemory(), ct);
            isEndOfMessage = result.EndOfMessage;
        }
        var json = Encoding.UTF8.GetString(memoryStream.ToArray());
        var content = JsonSerializer.Deserialize<RequestResponse>(json);
        return IsNotNull(content);
    }
}

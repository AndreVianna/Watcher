namespace Watcher.Common.ValueObjects;

public sealed class RemoteDataServer : IRemoteDataServer {
    private readonly ILogger<RemoteDataServer> _logger;

    private readonly IPEndPoint _localTcpEndPoint;
    private readonly TcpListener _tcpListener;

    private readonly IPEndPoint _localUdpEndPoint;
    private readonly UdpClient _udpClient;

    private readonly CancellationTokenSource _serverControl = new();
    private CancellationTokenSource _listeningControl = default!;
    private CancellationTokenSource _streamingControl = default!;

    public event AsyncEventHandler<ServerEventArgs>? OnServerStarted;
    public event AsyncEventHandler<ClientEventArgs>? OnClientConnected; // Happens only for TCP
    public event AsyncEventHandler<StreamEventArgs>? OnStreamingStarted; // Happens for both TCP and UDP
    public event AsyncEventHandler<StreamEventArgs>? OnDataChunkSent; // Happens for both TCP and UDP data streaming, where object is TContent
    public event AsyncEventHandler<StreamEventArgs>? OnDataChunkReceived; // Happens for both TCP and UDP data streaming, where object is TContent
    public event AsyncEventHandler<MessageEventArgs>? OnMessageSent; // Happens only for UDP, object will be of type TData
    public event AsyncEventHandler<MessageEventArgs>? OnRequestSent; // Happens only for TCP, object will be of type TRequest
    public event AsyncEventHandler<MessageEventArgs>? OnResponseReceived; // Happens only for TCP, object will be of type TResponse
    public event AsyncEventHandler? OnStreamingStopped; // Happens for both TCP and UDP
    public event AsyncEventHandler<ClientEventArgs>? OnClientDisconnected; // Happens only for TCP
    public event AsyncEventHandler? OnServerStopped;

    public RemoteDataServer(IConfiguration configuration, ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<RemoteDataServer>();
        var udpAddress = Ensure.IsNotNull(configuration["RemoteDataServer:UdpEndpoint:Address"]);
        _localUdpEndPoint = IPEndPoint.Parse(udpAddress);
        _udpClient = new(_localUdpEndPoint);
        var tcpAddress = Ensure.IsNotNull(configuration["RemoteDataServer:TcpEndpoint:Address"]);
        _localTcpEndPoint = IPEndPoint.Parse(tcpAddress);
        _tcpListener = new(_localTcpEndPoint);
        StartServer();
    }

    public void Dispose() {
        StopServer();
        ClearEvents();
        _udpClient.Dispose();
    }

    private void ClearEvents() {
        OnServerStarted = null;
        OnClientConnected = null;
        OnStreamingStarted = null;
        OnDataChunkReceived = null;
        OnMessageSent = null;
        OnStreamingStopped = null;
        OnServerStopped = null;
    }

    public bool IsRunning { get; private set; }

    public void StartListening() {
        try {
            _listeningControl = CancellationTokenSource.CreateLinkedTokenSource(_serverControl.Token);
            Task.Run(async () => {
                _tcpListener.Start();
                while (!_listeningControl.IsCancellationRequested) {
                    using var client = await _tcpListener.AcceptTcpClientAsync(_listeningControl.Token);
                    await ProcessClientMessage(client);
                }
            }, _listeningControl.Token)
                .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex);
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Listening at {ServerAddress} has stopped.", _tcpListener.Server.LocalEndPoint);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while listening at {ServerAddress}.", _tcpListener.Server.LocalEndPoint);
            throw;
        }
        finally {
            IsRunning = false;
            CallAsyncEvents(OnServerStopped, _listeningControl).FireAndForget();
            _logger.LogDebug("Listening at {ServerAddress} stopped.", _tcpListener.Server.LocalEndPoint);
        }
    }

    public void StopListening() {
        if (!IsRunning) return;
        _listeningControl.Cancel();
        _listeningControl = default!;
    }

    public async Task Broadcast<TData>(IEnumerable<IPEndPoint> remoteAddresses, TData data, CancellationToken ct)
        where TData : notnull {
        try {
            await Task.WhenAll(remoteAddresses.Select(ra => Send(ra, data, ct)));
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Broadcasting was canceled.");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while broadcasting.");
            throw;
        }
    }

    public async Task Send<TData>(IPEndPoint remoteAddress, TData data, CancellationToken ct)
        where TData : notnull {
        try {
            _logger.LogDebug("Sending data to {RemoteAddress}...", remoteAddress);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
            await _udpClient.SendAsync(bytes.AsMemory(), _serverControl.Token);
            var eventArgs = new MessageEventArgs {
                EndPoint = remoteAddress,
                Content = data,
            };
            CallAsyncEvents(OnMessageSent, eventArgs, _serverControl).FireAndForget();
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Sending data to {RemoteAddress} was canceled.", remoteAddress);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while sending data to {RemoteAddress}.", remoteAddress);
            throw;
        }
    }

    public async Task<TResponse?> Request<TRequest, TResponse>(IPEndPoint remoteAddress, TRequest request, CancellationToken ct)
        where TRequest : notnull {
        try {
            _logger.LogDebug("Sending request to {RemoteAddress}...", remoteAddress);
            var requestControl = CancellationTokenSource.CreateLinkedTokenSource(_serverControl.Token, ct);
            using var client = new TcpClient();
            await client.ConnectAsync(remoteAddress, requestControl.Token);
            await using var stream = client.GetStream();
            var requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
            await stream.WriteAsync(requestBytes.AsMemory(), requestControl.Token);
            var eventArgs = new MessageEventArgs {
                EndPoint = remoteAddress,
                Content = request,
            };
            CallAsyncEvents(OnRequestSent, eventArgs, requestControl).FireAndForget();
            _logger.LogDebug("Data sent to {RemoteAddress}.", remoteAddress);
            while (client.Available == 0 && !requestControl.IsCancellationRequested) {
                await Task.Delay(1, requestControl.Token);
            }
            var response = await JsonSerializer.DeserializeAsync<TResponse>(stream, cancellationToken: requestControl.Token);
            eventArgs = eventArgs with {
                Content = response,
            };
            CallAsyncEvents(OnResponseReceived, eventArgs, requestControl).FireAndForget();
            return response;
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Sending data to {RemoteAddress} was canceled.", remoteAddress);
            return default;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while sending data to {RemoteAddress}...", remoteAddress);
            throw;
        }
    }

    public bool IsStreaming { get; private set; }
    public void StartStreaming<TContent>(IPEndPoint remoteAddress, StreamType streamType, Func<CancellationToken, Task<StreamData<TContent>>> getData) {
        try {
            if (IsStreaming) return;
            IsStreaming = true;
            _streamingControl = CancellationTokenSource.CreateLinkedTokenSource(_serverControl.Token);
            _logger.LogDebug("Start streaming data to {RemoteAddress}...", remoteAddress);
            if (streamType == StreamType.Assured) {
                StreamUsingTcp(remoteAddress, getData)
                   .FireAndForget(onCancel: ex => throw ex,
                                  onException: ex => throw ex);
            }
            else {
                StreamUsingUdp(remoteAddress, getData)
                   .FireAndForget(onCancel: ex => throw ex,
                                  onException: ex => throw ex);
            }
            CallAsyncEvents(OnStreamingStopped, _streamingControl).FireAndForget();
            _logger.LogDebug("Streaming data to {RemoteAddress} stopped.", remoteAddress);
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Streaming data to {RemoteAddress} was canceled.", remoteAddress);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while streaming data to {RemoteAddress}.", remoteAddress);
            throw;
        }
    }

    private async Task ProcessClientMessage(TcpClient remoteClient) {
        var remoteAddress = IPEndPoint.Parse(remoteClient.Client.RemoteEndPoint!.Serialize().ToString());
        _logger.LogDebug("Client connected from {RemoteAddress}.", remoteAddress);
        var clientDetails = new ClientEventArgs {
            AssuredEndPoint = remoteAddress,
        };
        CallAsyncEvents(OnClientConnected, clientDetails, _listeningControl).FireAndForget();
        await using var stream = remoteClient.GetStream();
        var receivingControl = CancellationTokenSource.CreateLinkedTokenSource(_listeningControl.Token);
        while (!receivingControl.IsCancellationRequested) {
            if (!stream.DataAvailable) continue;
            var buffer = new byte[remoteClient.Available];
            var size = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), receivingControl.Token);
            var streamDetails = new StreamEventArgs {
                EndPoint = remoteAddress,
                Type = StreamType.Assured,
                Content = buffer[..size],
                IsEndOfData = !stream.DataAvailable,
            };
            CallAsyncEvents(OnDataChunkReceived, streamDetails, receivingControl).FireAndForget(onCancel: ex => throw ex);
        }
        CallAsyncEvents(OnClientDisconnected, clientDetails, _serverControl).FireAndForget();
    }

    private async Task StreamUsingTcp<TContent>(IPEndPoint remoteAddress, Func<CancellationToken, Task<StreamData<TContent>>> getData) {
        using var client = new TcpClient();
        await client.ConnectAsync(remoteAddress, _streamingControl.Token);
        var details = new StreamEventArgs {
            EndPoint = remoteAddress,
            Type = StreamType.Assured,
            Content = null,
            IsEndOfData = false,
        };
        CallAsyncEvents(OnStreamingStarted, details, _streamingControl)
           .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex);
        await using var stream = client.GetStream();
        while (!_streamingControl.IsCancellationRequested) {
            var chunk = await getData(_streamingControl.Token);
            var data = chunk.Content switch {
                byte[] bytes => bytes.AsMemory(),
                _ => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(chunk.Content)).AsMemory(),
            };
            await stream.WriteAsync(data, _streamingControl.Token);
            var chunkDetails = details with {
                Content = chunk.Content,
                IsEndOfData = chunk.IsEndOfData,
            };
            CallAsyncEvents(OnDataChunkSent, chunkDetails, _streamingControl)
               .FireAndForget(onCancel: ex => throw ex);
            if (chunk.IsEndOfData) break;
        }
        CallAsyncEvents(OnStreamingStopped, _serverControl).FireAndForget();
    }

    private async Task StreamUsingUdp<TContent>(IPEndPoint remoteAddress, Func<CancellationToken, Task<StreamData<TContent>>> getData) {
        var details = new StreamEventArgs {
            EndPoint = remoteAddress,
            Type = StreamType.Fast,
            Content = null,
            IsEndOfData = false,
        };
        CallAsyncEvents(OnStreamingStarted, details, _streamingControl)
           .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex);
        while (!_streamingControl.IsCancellationRequested) {
            var chunk = await getData(_streamingControl.Token);
            var data = chunk.Content switch {
                byte[] bytes => bytes.AsMemory(),
                _ => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(chunk.Content)).AsMemory(),
            };
            await _udpClient.SendAsync(data, _streamingControl.Token);
            var chunkDetails = details with {
                Content = chunk.Content,
                IsEndOfData = chunk.IsEndOfData,
            };
            CallAsyncEvents(OnDataChunkSent, chunkDetails, _streamingControl)
               .FireAndForget(onCancel: ex => throw ex);
            if (chunk.IsEndOfData) break;
        }
        CallAsyncEvents(OnStreamingStopped, _serverControl).FireAndForget();
    }

    public void StopStreaming() {
        if (!IsStreaming) return;
        _streamingControl.Cancel();
        _streamingControl = default!;
        IsStreaming = false;
    }

    private void StartServer() {
        if (IsRunning) return;
        _logger.LogDebug("Starting server...");
        _listeningControl = new();
        var details = new ServerEventArgs {
            AssuredEndPoint = _localTcpEndPoint,
            FastEndPoint = _localUdpEndPoint,
        };
        CallAsyncEvents(OnServerStarted, details, _serverControl).FireAndForget();
        IsRunning = true;
        _logger.LogDebug("Server started.");
    }

    private void StopServer() {
        if (!IsRunning) return;
        _logger.LogDebug("Stopping server...");
        _streamingControl.Cancel();
        _listeningControl.Cancel();
        _tcpListener.Stop();
        IsRunning = false;
        CallAsyncEvents(OnServerStopped, _listeningControl).FireAndForget();
        _logger.LogDebug("Server stopped.");
    }

    private static Task CallAsyncEvents(AsyncEventHandler? handler, CancellationTokenSource cts) {
        var events = (handler?
                     .GetInvocationList()
                     .Cast<AsyncEventHandler>()
                   ?? Enumerable.Empty<AsyncEventHandler>())
           .Select(e => e.Invoke(cts));
        return Task.WhenAll(events);
    }

    private static Task CallAsyncEvents<TArguments>(AsyncEventHandler<TArguments>? handler, TArguments args, CancellationTokenSource cts) {
        var events = (handler?
                     .GetInvocationList()
                     .Cast<AsyncEventHandler<TArguments>>()
                   ?? Enumerable.Empty<AsyncEventHandler<TArguments>>())
           .Select(e => e.Invoke(args, cts));
        return Task.WhenAll(events);
    }
}

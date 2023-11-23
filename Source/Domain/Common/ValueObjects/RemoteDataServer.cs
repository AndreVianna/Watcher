namespace Watcher.Common.ValueObjects;

public sealed class RemoteDataServer : IRemoteDataServer {
    private readonly ILogger<RemoteDataServer> _logger;
    private readonly HashSet<IPEndPoint> _connectedClients = new();

    private readonly IPEndPoint _localTcpEndPoint;
    private TcpListener? _tcpListener;

    private readonly IPEndPoint _localUdpEndPoint;
    private UdpClient? _udpClient;

    private CancellationTokenSource _serverControl = new();
    private CancellationTokenSource _streamingControl = new();

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
        var tcpAddress = Ensure.IsNotNull(configuration["RemoteDataServer:TcpEndpoint:Address"]);
        _localTcpEndPoint = IPEndPoint.Parse(tcpAddress);
        var udpAddress = Ensure.IsNotNull(configuration["RemoteDataServer:UdpEndpoint:Address"]);
        _localUdpEndPoint = IPEndPoint.Parse(udpAddress);
    }

    public void Dispose() {
        StopServer();
        ClearEvents();
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
    public async Task Start(CancellationToken ct) {
        try {
            if (IsRunning) return;
            _serverControl = CancellationTokenSource.CreateLinkedTokenSource(ct);
            StartServer();
            IsRunning = true;
            while (!_serverControl.IsCancellationRequested) {
                var client = await _tcpListener!.AcceptTcpClientAsync(_serverControl.Token);
                ProcessClientMessage(client).FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex);
            }
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Listening at {ServerAddress} has stopped.", _tcpListener!.Server.LocalEndPoint);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while listening at {ServerAddress}...", _tcpListener!.Server.LocalEndPoint);
            StopServer();
            throw;
        }
        finally {
            IsRunning = false;
            CallAsyncEvents(OnServerStopped, _serverControl)
               .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex); ;
            _logger.LogDebug("Listening at {ServerAddress} stopped.", _tcpListener!.Server.LocalEndPoint);
        }
    }

    private async Task ProcessClientMessage(TcpClient remoteClient) {
        var remoteAddress = IPEndPoint.Parse(remoteClient.Client.RemoteEndPoint!.Serialize().ToString());
        try {
            _connectedClients.Add(remoteAddress);
            _logger.LogDebug("Client connected from {RemoteAddress}.", remoteAddress);
            var clientDetails = new ClientEventArgs {
                AssuredEndPoint = remoteAddress,
            };
            CallAsyncEvents(OnClientConnected, clientDetails, _serverControl)
               .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex); ;
            await using var stream = remoteClient.GetStream();
            var receivingControl = CancellationTokenSource.CreateLinkedTokenSource(_serverControl.Token);
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
                CallAsyncEvents(OnDataChunkReceived, streamDetails, receivingControl)
                    .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex);
            }
            _connectedClients.Remove(remoteAddress);
            CallAsyncEvents(OnClientDisconnected, clientDetails, _serverControl)
               .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex); ;
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Receiving data from {RemoteAddress} has stopped.", remoteAddress);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while receiving data from {RemoteAddress}...", remoteAddress);
        }
        finally {
            remoteClient.Dispose();
        }
    }

    public void Stop() {
        if (!IsRunning) return;
        _serverControl.Cancel();
    }

    public async Task Send<TData>(IPEndPoint remoteAddress, TData data, bool stopServerOnError, CancellationToken ct)
        where TData : notnull {
        try {
            _logger.LogDebug("Sending data to {RemoteAddress}...", remoteAddress);
            var sendDataControl = CancellationTokenSource.CreateLinkedTokenSource(_serverControl.Token, ct);
            using var client = new UdpClient();
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
            await client.SendAsync(bytes.AsMemory(), sendDataControl.Token);
            var eventArgs = new MessageEventArgs {
                EndPoint = remoteAddress,
                Content = data,
            };
            CallAsyncEvents(OnMessageSent, eventArgs, sendDataControl)
               .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex);
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Sending data to {RemoteAddress} was canceled.", remoteAddress);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while sending data to {RemoteAddress}...", remoteAddress);
            if (!stopServerOnError)
                StopServer();
            throw;
        }
    }

    public async Task<TResponse?> Request<TRequest, TResponse>(IPEndPoint remoteAddress, TRequest request, bool stopServerOnError, CancellationToken ct)
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
            CallAsyncEvents(OnRequestSent, eventArgs, requestControl)
               .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex);
            _logger.LogDebug("Data sent to {RemoteAddress}.", remoteAddress);
            var response = await JsonSerializer.DeserializeAsync<TResponse>(stream, cancellationToken: requestControl.Token);
            eventArgs = eventArgs with {
                Content = response,
            };
            CallAsyncEvents(OnResponseReceived, eventArgs, requestControl)
               .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex);
            return response;
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Sending data to {RemoteAddress} was canceled.", remoteAddress);
            return default;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while sending data to {RemoteAddress}...", remoteAddress);
            if (!stopServerOnError)
                StopServer();
            throw;
        }
    }

    public bool IsStreaming { get; private set; }
    public async Task StartStreaming<TContent>(IPEndPoint remoteAddress, StreamType streamType, Func<CancellationToken, Task<StreamData<TContent>>> getData, bool stopServerOnError, CancellationToken ct) {
        try {
            if (IsStreaming) return;
            IsStreaming = true;
            _streamingControl = CancellationTokenSource.CreateLinkedTokenSource(_serverControl.Token, ct);
            _logger.LogDebug("Start streaming data to {RemoteAddress}...", remoteAddress);
            if (streamType == StreamType.Assured)
                await StreamUsingTcp(remoteAddress, getData);
            else
                await StreamUsingUdp(remoteAddress, getData);
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Streaming data to {RemoteAddress} was canceled.", remoteAddress);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while streaming data to {RemoteAddress}...", remoteAddress);
            if (stopServerOnError) {
                StopServer();
                throw;
            }
        }
        finally {
            CallAsyncEvents(OnStreamingStopped, _streamingControl)
               .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex); ;
            _logger.LogDebug("Streaming data to {RemoteAddress} stopped.", remoteAddress);
        }
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
           .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex); ;
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
               .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex); ;
            if (chunk.IsEndOfData) break;
        }
        CallAsyncEvents(OnStreamingStopped, _serverControl)
           .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex); ;
    }

    private async Task StreamUsingUdp<TContent>(IPEndPoint remoteAddress, Func<CancellationToken, Task<StreamData<TContent>>> getData) {
        using var client = new UdpClient();
        var details = new StreamEventArgs {
            EndPoint = remoteAddress,
            Type = StreamType.Fast,
            Content = null,
            IsEndOfData = false,
        };
        CallAsyncEvents(OnStreamingStarted, details, _streamingControl)
           .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex); ;
        while (!_streamingControl.IsCancellationRequested) {
            var chunk = await getData(_streamingControl.Token);
            var data = chunk.Content switch {
                byte[] bytes => bytes.AsMemory(),
                _ => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(chunk.Content)).AsMemory(),
            };
            await client.SendAsync(data, _streamingControl.Token);
            var chunkDetails = details with {
                Content = chunk.Content,
                IsEndOfData = chunk.IsEndOfData,
            };
            CallAsyncEvents(OnDataChunkSent, chunkDetails, _streamingControl)
               .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex); ;
            if (chunk.IsEndOfData) break;
        }
        CallAsyncEvents(OnStreamingStopped, _serverControl)
           .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex); ;
    }

    public void StopStreaming() {
        if (!IsStreaming) return;
        _streamingControl.Cancel();
        IsStreaming = false;
    }

    private void StartServer() {
        _logger.LogDebug("Starting server...");
        _udpClient = new(_localTcpEndPoint);
        _tcpListener = new(_localTcpEndPoint);
        _tcpListener.Start();
        var details = new ServerEventArgs {
            AssuredEndPoint = _localTcpEndPoint,
            FastEndPoint = _localUdpEndPoint,
        };
        CallAsyncEvents(OnServerStarted, details, _serverControl)
           .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex);
        _logger.LogDebug("Server started.");
    }

    private void StopServer() {
        _logger.LogDebug("Stopping server...");
        StopStreaming();
        Stop();
        _tcpListener!.Stop();
        CallAsyncEvents(OnServerStopped, _serverControl)
           .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex); ;
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

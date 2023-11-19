namespace Watcher.Common.ValueObjects;

public sealed class TcpServer : ITcpServer {
    private readonly ILogger<TcpServer> _logger;

    private TcpListener _server = default!;
    private CancellationTokenSource _listeningControl = new();
    private CancellationTokenSource _streamingControl = new();

    public event Action? OnServerStated;
    public event AsyncEventHandler? OnListeningStarted;
    public event AsyncEventHandler<string>? OnClientConnected;
    public event AsyncEventHandler<string>? OnStreamingStarted;
    public event AsyncEventHandler<(string, DataPackage)>? OnDataReceived;
    public event AsyncEventHandler<(string, DataPackage)>? OnDataSent;
    public event AsyncEventHandler<(string, DataPackage)>? OnDataStreamed;
    public event Action? OnStreamingStopped;
    public event Action? OnListeningStopped;
    public event Action? OnServerStopped;

    public TcpServer(ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<TcpServer>();
    }

    public void Dispose() {
        Stop();
        ClearEvents();
    }

    private void ClearEvents() {
        OnServerStated = null;
        OnListeningStarted = null;
        OnStreamingStarted = null;
        OnDataReceived = null;
        OnDataSent = null;
        OnStreamingStopped = null;
        OnListeningStopped = null;
        OnServerStopped = null;
    }

    public bool IsListening { get; private set; }
    public async Task StartListening(string localAddress, bool stopServerOnError, CancellationToken ct) {
        try {
            if (IsListening) return;
            Start(localAddress);
            _listeningControl = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _logger.LogDebug("Start listening at {ServerAddress}...", _server.Server.LocalEndPoint);
            await CallAsyncEvents(OnListeningStarted, _listeningControl);
            IsListening = true;
            while (!_listeningControl.IsCancellationRequested) {
                var client = await _server.AcceptTcpClientAsync(_listeningControl.Token);
                ProcessConnectedClient(client).FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex);
            }
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Listening at {ServerAddress} has stopped.", _server.Server.LocalEndPoint);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while listening at {ServerAddress}...", _server.Server.LocalEndPoint);
            if (stopServerOnError) {
                Stop();
                throw;
            }
        }
        finally {
            IsListening = false;
            OnListeningStopped?.Invoke();
            _logger.LogDebug("Listening at {ServerAddress} stopped.", _server.Server.LocalEndPoint);
        }
    }

    private async Task ProcessConnectedClient(TcpClient remoteClient) {
        var remoteAddress = $"{remoteClient.Client.RemoteEndPoint}";
        try {
            _logger.LogDebug("Client connected from {RemoteAddress}.", remoteAddress);
            await CallAsyncEvents(OnClientConnected, remoteAddress, _listeningControl);
            await using var stream = remoteClient.GetStream();
            var clientConnectionControl = new CancellationTokenSource();
            while (!_listeningControl.IsCancellationRequested && !clientConnectionControl.IsCancellationRequested) {
                if (!stream.DataAvailable) continue;
                var buffer = new byte[remoteClient.Available];
                var size = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), clientConnectionControl.Token);
                var data = new DataPackage() {
                    Bytes = buffer[..size],
                    IsEndOfData = !stream.DataAvailable,
                };
                var eventArgs = (remoteAddress, data);
                CallAsyncEvents(OnDataReceived, eventArgs, clientConnectionControl)
                    .FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex);
            }
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

    public void StopListening() {
        if (!IsListening) return;
        _listeningControl.Cancel();
    }

    public async Task SendData(string remoteAddress, byte[] data, bool stopServerOnError, CancellationToken ct) {
        try {
            var remoteUri = new Uri(remoteAddress);
            _logger.LogDebug("Sending data to {RemoteAddress}...", remoteAddress);
            var sendDataControl = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Parse(remoteAddress), remoteUri.Port, sendDataControl.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync(data.AsMemory(), sendDataControl.Token);
            var eventArgs = (remoteAddress, new DataPackage {
                Bytes = data,
                IsEndOfData = true,
            });
            await CallAsyncEvents(OnDataSent, eventArgs, sendDataControl);
            _logger.LogDebug("Data sent to {RemoteAddress}.", remoteAddress);
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Sending data to {RemoteAddress} was canceled.", remoteAddress);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while sending data to {RemoteAddress}...", remoteAddress);
            if (stopServerOnError) {
                Stop();
                throw;
            }
        }
    }

    public bool IsStreaming { get; private set; }
    public async Task StartStreaming(string remoteAddress, Func<CancellationToken, Task<DataPackage>> getData, bool stopOnError, CancellationToken ct) {
        try {
            var remoteUri = new Uri(remoteAddress);
            if (IsStreaming) return;
            IsStreaming = true;
            _streamingControl = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _logger.LogDebug("Start streaming data to {RemoteAddress}...", remoteAddress);
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Parse(remoteAddress), remoteUri.Port, _streamingControl.Token);
            await CallAsyncEvents(OnStreamingStarted, remoteAddress, _listeningControl);
            await using var stream = client.GetStream();
            while (!_streamingControl.IsCancellationRequested) {
                var chunk = await getData(_listeningControl.Token);
                await stream.WriteAsync(chunk.Bytes.AsMemory(), _streamingControl.Token);
                var eventArgs = (remoteAddress, chunk);
                CallAsyncEvents(OnDataStreamed, eventArgs, _streamingControl).FireAndForget();
                if (chunk.IsEndOfData) break;
            }
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Streaming data to {RemoteAddress} was canceled.", remoteAddress);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while streaming data to {RemoteAddress}...", remoteAddress);
            if (stopOnError) throw;
        }
        finally {
            OnStreamingStopped?.Invoke();
            _logger.LogDebug("Streaming data to {RemoteAddress} stopped.", remoteAddress);
            if (stopOnError) Stop();
        }
    }
    public void StopStreaming() {
        if (!IsStreaming) return;
        _streamingControl.Cancel();
        IsStreaming = false;
    }

    private void Start(string localAddress) {
        var serverUri = new Uri(localAddress);
        _logger.LogDebug("Starting server at {ServerAddress}...", localAddress);
        _server = new(IPAddress.Parse(serverUri.Host), serverUri.Port);
        _server.Start();
        OnServerStated?.Invoke();
        _logger.LogDebug("Server started.");
    }

    private void Stop() {
        _logger.LogDebug("Stopping server at {RemoteAddress}...", _server.Server.LocalEndPoint);
        StopListening();
        StopStreaming();
        _server.Stop();
        OnServerStopped?.Invoke();
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

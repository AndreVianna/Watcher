using System.Diagnostics.CodeAnalysis;

namespace Watcher.Common.ValueObjects;

public sealed class RemoteDataServer : IRemoteDataServer {
    private readonly ILogger<RemoteDataServer> _logger;
    private readonly IConfiguration _configuration;
    private HashSet<IPEndPoint> _connectedClients = new HashSet<IPEndPoint>();

    private IPEndPoint _localTcpEndPoint;
    private TcpListener? _tcpListener;

    private IPEndPoint _localUdpEndPoint;
    private UdpClient? _udpClient;

    private CancellationTokenSource _serverControl = new();
    private CancellationTokenSource _streamingControl = new();

    public event AsyncEventHandler<RemoteDataServerDetails>? OnServerStarted;
    public event AsyncEventHandler<ClientDetails>? OnClientConnected; // Happens only for TCP
    public event AsyncEventHandler<StreamDetails>? OnStreamingStarted; // Happens for both TCP and UDP
    public event AsyncEventHandler<StreamDetails>? OnDataChunkSent; // Happens for both TCP and UDP data streaming, where object is TContent
    public event AsyncEventHandler<StreamDetails>? OnDataChunkReceived; // Happens for both TCP and UDP data streaming, where object is TContent
    public event AsyncEventHandler<MessageDetails>? OnDataSent; // Happens only for UDP, object will be of type TData
    public event AsyncEventHandler<MessageDetails>? OnRequestSent; // Happens only for TCP, object will be of type TRequest
    public event AsyncEventHandler<MessageDetails>? OnResponseReceived; // Happens only for TCP, object will be of type TResponse
    public event AsyncEventHandler? OnStreamingStopped; // Happens for both TCP and UDP
    public event AsyncEventHandler<ClientDetails>? OnClientDisconnected; // Happens only for TCP
    public event AsyncEventHandler? OnServerStopped;

    public RemoteDataServer(IConfiguration configuration, ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<RemoteDataServer>();
        _configuration = configuration;
        var tcpAddress = Ensure.IsNotNull(configuration["RemoteDataServer:TcpEndpoint:Address"]);
        var tcpPort = Convert.ToInt32(Ensure.IsNotNull(configuration["RemoteDataServer:TcpEndpoint:Port"]));
        _localTcpEndPoint = new IPEndPoint(IPAddress.Parse(tcpAddress), tcpPort);
        var udpAddress = Ensure.IsNotNull(configuration["RemoteDataServer:UdpEndpoint:Address"]);
        var udpPort = Convert.ToInt32(Ensure.IsNotNull(configuration["RemoteDataServer:UdpEndpoint:Port"]));
        _localUdpEndPoint = new IPEndPoint(IPAddress.Parse(udpAddress), udpPort);
    }

    public async ValueTask DisposeAsync() {
        await StopServer();
        ClearEvents();
    }

    private void ClearEvents() {
        OnServerStarted = null;
        OnClientConnected = null;
        OnStreamingStarted = null;
        OnDataChunkReceived = null;
        OnDataSent = null;
        OnStreamingStopped = null;
        OnServerStopped = null;
    }

    public bool IsRunning{ get; private set; }
    public async Task Start(CancellationToken ct) {
        try {
            if (IsRunning) return;
            _serverControl = CancellationTokenSource.CreateLinkedTokenSource(ct);
            await StartServer();
            IsRunning = true;
            while (!_serverControl.IsCancellationRequested) {
                var client = await _tcpListener!.AcceptTcpClientAsync(_serverControl.Token);
                ProcessConnectedClient(client).FireAndForget(onCancel: ex => throw ex, onException: ex => throw ex);
            }
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Listening at {ServerAddress} has stopped.", _tcpListener!.Server.LocalEndPoint);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while listening at {ServerAddress}...", _tcpListener!.Server.LocalEndPoint);
            await StopServer();
            throw;
        }
        finally {
            IsRunning = false;
            await CallAsyncEvents(OnServerStopped, _serverControl);
            _logger.LogDebug("Listening at {ServerAddress} stopped.", _tcpListener!.Server.LocalEndPoint);
        }
    }

    private async Task ProcessConnectedClient(TcpClient remoteClient) {
        var remoteAddress = IPEndPoint.Parse(remoteClient.Client.RemoteEndPoint!.Serialize().ToString());
        try {
            _logger.LogDebug("Client connected from {RemoteAddress}.", remoteAddress);
            var clientDetails = new ClientDetails {
                AssuredEndPoint = remoteAddress,
            };
            await CallAsyncEvents(OnClientConnected, clientDetails, _serverControl);
            await using var stream = remoteClient.GetStream();
            var clientConnectionControl = new CancellationTokenSource();
            while (!_serverControl.IsCancellationRequested && !clientConnectionControl.IsCancellationRequested) {
                if (!stream.DataAvailable) continue;
                var buffer = new byte[remoteClient.Available];
                var size = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), clientConnectionControl.Token);
                var streamDetails = new StreamDetails() {
                    EndPoint = remoteAddress,
                    Type = StreamType.Assured,
                    Content = buffer[..size],
                    IsEndOfData = !stream.DataAvailable,
                };
                CallAsyncEvents(OnDataChunkReceived, streamDetails, clientConnectionControl)
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

    public void Stop() {
        if (!IsRunning) return;
        _serverControl.Cancel();
    }

    public async Task Send<TData>(IPEndPoint remoteAddress, TData data, bool stopServerOnError, CancellationToken ct)
        where TData : notnull {
        try {
            _logger.LogDebug("Sending data to {RemoteAddress}...", remoteAddress);
            var sendDataControl = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var client = new TcpClient();
            await client.ConnectAsync(remoteAddress, sendDataControl.Token);
            await using var stream = client.GetStream();
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
            await stream.WriteAsync(bytes.AsMemory(), sendDataControl.Token);
            var eventArgs = new MessageDetails() {
                EndPoint = remoteAddress,
                Content = data,
            };
            await CallAsyncEvents(OnDataSent, eventArgs, sendDataControl);
            _logger.LogDebug("Data sent to {RemoteAddress}.", remoteAddress);
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Sending data to {RemoteAddress} was canceled.", remoteAddress);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while sending data to {RemoteAddress}...", remoteAddress);
            if (stopServerOnError) {
                await StopServer();
                throw;
            }
        }
    }

    public Task<TResponse> Request<TRequest, TResponse>(IPEndPoint remoteAddress, TRequest request, bool stopServerOnError, CancellationToken ct)
        => throw new NotImplementedException();

    public bool IsStreaming { get; private set; }
    public async Task StartStreaming<TContent>(IPEndPoint remoteAddress, StreamType streamType, Func<CancellationToken, Task<StreamData<TContent>>> getData, bool stopServerOnError, CancellationToken ct) {
        try {
            if (IsStreaming) return;
            IsStreaming = true;
            _streamingControl = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _logger.LogDebug("Start streaming data to {RemoteAddress}...", remoteAddress);
            using var client = new TcpClient();
            await client.ConnectAsync(remoteAddress, _streamingControl.Token);
            var details = new StreamDetails {
                EndPoint = remoteAddress,
                Type = streamType,
                Content = null,
                IsEndOfData = false,
            };
            await CallAsyncEvents(OnStreamingStarted, details, _serverControl);
            await using var stream = client.GetStream();
            while (!_streamingControl.IsCancellationRequested) {
                var chunk = await getData(_serverControl.Token);
                var data = chunk.Content switch {
                    byte[] bytes => bytes.AsMemory(),
                    _ => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(chunk.Content)).AsMemory(),
                };
                await stream.WriteAsync(data, _streamingControl.Token);
                var chunkDetails = details with {
                    Content = chunk.Content,
                    IsEndOfData = chunk.IsEndOfData,
                };
                CallAsyncEvents(OnDataChunkSent, chunkDetails, _streamingControl).FireAndForget();
                if (chunk.IsEndOfData) break;
            }
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Streaming data to {RemoteAddress} was canceled.", remoteAddress);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while streaming data to {RemoteAddress}...", remoteAddress);
            if (stopServerOnError) {
                await StopServer();
                throw;
            }
        }
        finally {
            await CallAsyncEvents(OnStreamingStopped, _streamingControl);
            _logger.LogDebug("Streaming data to {RemoteAddress} stopped.", remoteAddress);
        }
    }
    public void StopStreaming() {
        if (!IsStreaming) return;
        _streamingControl.Cancel();
        IsStreaming = false;
    }

    private async Task StartServer() {
        _logger.LogDebug("Starting server...");
        _udpClient = new(_localTcpEndPoint);
        _tcpListener = new(_localTcpEndPoint);
        _tcpListener.Start();
        var details = new RemoteDataServerDetails {
            AssuredEndPoint = _localTcpEndPoint,
            FastEndPoint = _localUdpEndPoint,
        };
        await CallAsyncEvents(OnServerStarted, details, _serverControl);
        _logger.LogDebug("Server started.");
    }

    private async Task StopServer() {
        _logger.LogDebug("Stopping server...");
        StopStreaming();
        Stop();
        _tcpListener!.Stop();
        await CallAsyncEvents(OnServerStopped, _serverControl);
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

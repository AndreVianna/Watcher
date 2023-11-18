namespace Watcher.WorkstationRegistration.ValueObjects;

public sealed class RemoteConnection : IRemoteConnection {
    private readonly ILogger<RemoteConnection> _logger;

    private readonly string _remoteAddress;
    private readonly ClientWebSocket _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receivingTask;

    public delegate Task ReceivedDataHandler(ArraySegment<byte> data, bool isEndOfData, CancellationToken ct);
    public event ReceivedDataHandler? OnReceivedData;

    public RemoteConnection(string remoteAddress, ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<RemoteConnection>();
        _remoteAddress = remoteAddress;
        _webSocket = new();
        _logger.LogDebug("Remote connection to {RemoteAddress} created.", _remoteAddress);
    }

    public async ValueTask DisposeAsync() {
        await Disconnect("Dispose");
        OnReceivedData = null;
        _webSocket.Dispose();
        _logger.LogDebug("Remote connection to {RemoteAddress} disposed.", _remoteAddress);
    }

    public Func<ArraySegment<byte>, bool, CancellationToken, Task> ProcessReceivedData { get; init; } = (_, _, _) => Task.CompletedTask;

    public async Task Connect(CancellationToken ct) {
        _logger.LogDebug("Connecting to {RemoteAddress}...", _remoteAddress);
        await _webSocket.ConnectAsync(new(_remoteAddress), ct);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _receivingTask = StartReceiving(_cts.Token);
        _logger.LogDebug("{RemoteAddress} connected.", _remoteAddress);
    }

    public async Task Disconnect(string reason, CancellationToken ct = default) {
        _logger.LogDebug("Disconnecting from {RemoteAddress}...", _remoteAddress);
        _cts?.Cancel();
        _receivingTask?.Dispose();
        _receivingTask = null;
        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, ct);
        _logger.LogDebug("{RemoteAddress} disconnected.", _remoteAddress);
    }

    public Task SendCommand(string command, CancellationToken ct) {
        _logger.LogDebug("Sending command '{Command}' to {RemoteAddress}...", command, _remoteAddress);
        var data = Encoding.UTF8.GetBytes(command);
        return _webSocket.SendAsync(data, WebSocketMessageType.Text, true, ct);
    }

    private async Task StartReceiving(CancellationToken ct) {
        try {
            _logger.LogDebug("Start receiving data from {RemoteAddress}...", _remoteAddress);
            var buffer = new ArraySegment<byte>(new byte[1024]);
            while (!ct.IsCancellationRequested && _webSocket.State == WebSocketState.Open) {
                var result = await _webSocket.ReceiveAsync(buffer, ct);

                if (ct.IsCancellationRequested || result.MessageType == WebSocketMessageType.Close) {
                    await Disconnect("Closed remotely.", ct);
                    break;
                }

                OnReceivedData?.Invoke(buffer, result.EndOfMessage, ct);
            }
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Streaming data from {RemoteAddress} was canceled.", _remoteAddress);
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred while receiving data from {RemoteAddress}...", _remoteAddress);
            throw;
        }
    }
}

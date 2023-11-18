namespace Watcher.Daemon.Services;

public sealed class WatcherService : BackgroundService {
    private readonly ILogger<WatcherService> _logger;

    private readonly IStreamer _streamer;
    private readonly IListener _listener;

    private bool _isDisposed;
    private CancellationTokenSource? _cts;

    public WatcherService(IListener listener, IStreamer streamer, ILogger<WatcherService> logger) {
        _logger = logger;
        _streamer = streamer;
        _listener = listener;
    }

    public override void Dispose() {
        if (_isDisposed) return;
        StopService();
        base.Dispose();
        _isDisposed = true;
        _logger.LogDebug("Service disposed.");
    }

    private void StopService() {
        _cts?.Cancel();
        _cts = null;

        _listener.Stop().Wait();
        _logger.LogDebug("Listener has stopped.");
        _streamer.Stop();
        _logger.LogDebug("Streamer has stopped.");
    }

    protected override async Task ExecuteAsync(CancellationToken ct) {
        try {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _logger.LogDebug("Service is starting.");
            _listener.OnDataReceived += ProcessRequest;
            await _listener.Start(ct);
            while (!ct.IsCancellationRequested) {
                await Task.Delay(100, _cts.Token);
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error while executing service.");
        }
        finally {
            StopService();
            _logger.LogDebug("Service has stopped.");
        }
    }

    private Task ProcessRequest(ArraySegment<byte> data, bool isEndOfData, CancellationToken ct)
        => isEndOfData ? ProcessMessageChunk(data, ct) : Task.CompletedTask;

    private Task ProcessMessageChunk(ReadOnlySpan<byte> data, CancellationToken ct) {
        var message = UTF8.GetString(data.ToArray()).Trim().ToLower();
        switch (message) {
            case "start":
                return _streamer.Start(ct);
            case "stop":
                _streamer.Stop();
                return Task.CompletedTask;
            default:
                _logger.LogWarning("Unrecognized command: {command}", message);
                return Task.CompletedTask;
        }
    }
}

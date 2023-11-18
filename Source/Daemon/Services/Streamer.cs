namespace Watcher.Daemon.Services;

public abstract class Streamer<TSelf> : IStreamer
    where TSelf : IStreamer {
    private readonly ILogger<TSelf> _logger;

    private CancellationTokenSource? _cts;
    private Task? _streamingTask;

    private bool _isDisposed;

    protected Streamer(ILogger<TSelf> logger) {
        _logger = logger;
    }

    protected virtual void Dispose(bool disposing) {
        if (!disposing) return;
        Reset();
    }

    public void Dispose() {
        if (_isDisposed) return;
        Dispose(true);
        GC.SuppressFinalize(this);
        _isDisposed = true;
    }

    public Task Start(WebSocket webSocket, CancellationToken ct) {
        if (_streamingTask is not null) {
            _logger.LogDebug("Data streaming is already running.");
            return _streamingTask;
        }

        _logger.LogDebug("Data streaming started.");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _streamingTask = SendData(webSocket, ct);

        return _streamingTask;
    }

    public void Stop() {
        if (_cts is null) {
            _logger.LogDebug("Data streaming is not running or already stopped.");
            return;
        }

        try {
            _logger.LogDebug("Data streaming stop signal sent.");
            _cts.Cancel();
        }
        finally {
            Reset();
        }
    }

    protected abstract Task ProcessData(WebSocket webSocket, CancellationToken ct);

    private async Task SendData(WebSocket webSocket, CancellationToken ct) {
        try {
            while (!ct.IsCancellationRequested && webSocket.State == WebSocketState.Open) {
                await ProcessData(webSocket, ct);
            }
        }
        catch (OperationCanceledException) {
            _logger.LogDebug("Streaming was canceled.");
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error occurred during data streaming.");
            throw;
        }
        finally {
            Reset();
        }
    }

    private void Reset() {
        _cts?.Dispose();
        _cts = null;
        _streamingTask?.Dispose();
        _streamingTask = null;
    }
}

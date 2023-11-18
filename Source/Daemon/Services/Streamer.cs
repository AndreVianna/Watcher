namespace Watcher.Daemon.Services;

public abstract class Streamer<TSelf> : IStreamer
    where TSelf : IStreamer {
    private readonly ILogger<TSelf> _logger;

    private CancellationTokenSource? _cts;
    private Task? _streamingTask;

    private readonly IRemoteConnection _remoteConnection;

    private bool _isDisposed;

    protected Streamer(IConfiguration configuration, ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<TSelf>();
        var baseAddress = IsNotNullOrWhiteSpace(configuration.GetValue<string>("Hub:BaseAddress"));
        _remoteConnection = new RemoteConnection(baseAddress, loggerFactory);
    }

    protected virtual async Task Dispose(bool disposing) {
        if (!disposing) return;
        Reset();
        await _remoteConnection.DisposeAsync();
    }

    public async ValueTask DisposeAsync() {
        if (_isDisposed) return;
        await Dispose(true);
        GC.SuppressFinalize(this);
        _isDisposed = true;
    }

    public Task Start(CancellationToken ct) {
        if (_streamingTask is not null) {
            _logger.LogDebug("Data streaming is already running.");
            return _streamingTask;
        }

        _logger.LogDebug("Data streaming started.");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _streamingTask = SendData(ct);

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

    protected abstract Task<DataPackage> GetData(CancellationToken ct);

    private async Task SendData(CancellationToken ct) {
        try {
            var package = await GetData(ct);
            while (!ct.IsCancellationRequested) {
                await _remoteConnection.SendData(package.Bytes, package.IsEndOfData, ct);
                package = await GetData(ct);
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

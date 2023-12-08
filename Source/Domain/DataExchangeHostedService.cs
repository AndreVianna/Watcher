namespace DotnetToolbox.Net;

public class DataExchangeHostedService : BackgroundService {
    private readonly ILogger _logger;
    private readonly IDataExchangeAgent _agent;

    private bool _isDisposed;
    private CancellationTokenSource? _cts;

    public DataExchangeHostedService(IDataExchangeAgent agent, ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger("Host");
        _logger.LogDebug("Creating data exchange host...");
        _agent = agent;
        _logger.LogDebug("Data exchange host created.");
    }

    public override void Dispose() {
        if (_isDisposed) return;
        _logger.LogDebug("Disposing data exchange host...");
        if (_agent.IsRunning) _agent.Stop();
        base.Dispose();
        _isDisposed = true;
        _logger.LogDebug("Data exchange host disposed.");
    }

    protected override async Task ExecuteAsync(CancellationToken ct) {
        _logger.LogInformation("Data exchange host is running.");
        try {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _agent.Start();
            while (!_cts.IsCancellationRequested) await Task.Delay(100, _cts.Token);
        }
        catch (OperationCanceledException ex) {
            _logger.LogError(ex, "Cancel requested.");
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error while executing agent.");
            throw;
        }
        finally {
            _agent.Stop();
            _logger.LogDebug("Service has stopped.");
        }
    }
}

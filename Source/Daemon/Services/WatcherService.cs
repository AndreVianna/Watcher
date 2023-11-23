namespace Watcher.Daemon.Services;

public sealed class WatcherService : BackgroundService {
    private readonly ILogger<WatcherService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IRemoteDataServer _server;

    private bool _isDisposed;
    private CancellationTokenSource? _cts;

    public WatcherService(IConfiguration configuration, IRemoteDataServer server, ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<WatcherService>();
        _configuration = configuration;
        _server = server;
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

        _server.StopListening();
        _logger.LogDebug("Service has stopped.");
    }

    protected override async Task ExecuteAsync(CancellationToken ct) {
        try {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _logger.LogDebug("Service is starting.");
            var serverAddress = IsNotNullOrWhiteSpace(_configuration["Watcher:Address"]);
            var serverPort = System.Convert.ToInt32(IsNotNullOrWhiteSpace(_configuration["Watcher:Port"]));
            _server.OnDataChunkReceived += ProcessRequest;
            _server.StartListening();
            while (!_cts.IsCancellationRequested) {
                await Task.Delay(100, _cts.Token);
            }
        }
        catch (OperationCanceledException ex) {
            _logger.LogError(ex, "Cancel requested.");
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error while executing service.");
            throw;
        }
        finally {
            StopService();
            _logger.LogDebug("Service has stopped.");
        }
    }

    private uint _counter = 1;
    private Task<StreamData<byte[]>> GenerateData(CancellationToken _) {
        _counter = _counter == 31 ? 0 : _counter++;
        return Task.FromResult(new StreamData<byte[]> {
            Content = "."u8.ToArray(),
            IsEndOfData = _counter == 0,
        });
    }

    private Task ProcessRequest(StreamEventArgs args, CancellationTokenSource cts) {
        var message = UTF8.GetString((byte[])args.Content!).Trim().ToLower();
        switch (message) {
            case "start":
                _server.StartStreaming(args.EndPoint, StreamType.Assured, GenerateData);
                return Task.CompletedTask;
            case "stop":
                _server.StopStreaming();
                return Task.CompletedTask;
            default:
                _logger.LogWarning("Unrecognized command: {command}", message);
                return Task.CompletedTask;
        }
    }
}

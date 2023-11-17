namespace Watcher.Daemon.Services;

public sealed class WatcherDaemonService : BackgroundService {
    private readonly ILogger<WatcherDaemonService> _logger;
    private readonly HttpListener _httpListener;
    private bool _isDisposed;
    private readonly int _bufferSize;

    public WatcherDaemonService(IConfiguration configuration, ILogger<WatcherDaemonService> logger) {
        _logger = logger;
        _httpListener = new();
        var baseAddress = IsNotNullOrWhiteSpace(configuration.GetValue<string>("Hub:BaseAddress"));
        _bufferSize = HasValue(configuration.GetValue<int?>("Hub:MessageBufferSize"));
        _httpListener.Prefixes.Add(baseAddress);
    }

    public override void Dispose() {
        if (_isDisposed) return;
        if (_httpListener.IsListening) {
            _httpListener.Stop();
            _logger.LogInformation("Service has stopped.");
        }
        _httpListener.Close();
        base.Dispose();
        _isDisposed = true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            _logger.LogInformation("Service is starting.");
            _httpListener.Start();
            if (!_httpListener.IsListening) throw new InvalidOperationException($"Failed to start listening to '{string.Join("', '", _httpListener.Prefixes)}'.");

            _logger.LogInformation("Listening on {address}", $"'{string.Join("', '", _httpListener.Prefixes)}'");

            while (!stoppingToken.IsCancellationRequested) {
                await ProcessRequest();
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error while listening on {address}", $"'{string.Join("', '", _httpListener.Prefixes)}'");
        }
        finally {
            if (_httpListener.IsListening) {
                _httpListener.Stop();
                _logger.LogInformation("Service has stopped.");
            }
        }
    }

    private async Task ProcessRequest() {
        var context = await _httpListener.GetContextAsync();
        if (context.Request.IsWebSocketRequest) {
            await HandleWebSocketAsync(context);
        }
        else {
            context.Response.StatusCode = 400;
            context.Response.Close();
        }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext httpContext) {
        var webSocketContext = await httpContext.AcceptWebSocketAsync(subProtocol: null);
        using var webSocket = webSocketContext.WebSocket;

        var buffer = new byte[_bufferSize];
        var result = await webSocket.ReceiveAsync(new(buffer), CancellationToken.None);
        while (!result.CloseStatus.HasValue) {
            await ProcessMessageChunk(buffer);
            result = await webSocket.ReceiveAsync(new(buffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }

    private Task ProcessMessageChunk(Span<byte> buffer) {
        _logger.LogDebug("Received {buffer.Length} byte.", buffer.Length);
        return Task.CompletedTask;
    }
}

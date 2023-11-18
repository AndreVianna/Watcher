namespace Watcher.Daemon.Services;

public sealed class WatcherService : BackgroundService {
    private readonly ILogger<WatcherService> _logger;
    private readonly HttpListener _httpListener;
    private bool _isDisposed;
    private readonly int _bufferSize;
    private readonly IStreamer _streamer;

    public WatcherService(IConfiguration configuration, IStreamer streamer, ILogger<WatcherService> logger) {
        _streamer = streamer;
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

    protected override async Task ExecuteAsync(CancellationToken ct) {
        try {
            _logger.LogInformation("Service is starting.");
            _httpListener.Start();
            if (!_httpListener.IsListening) throw new InvalidOperationException($"Failed to start listening to '{string.Join("', '", _httpListener.Prefixes)}'.");

            _logger.LogInformation("Listening on {address}", $"'{string.Join("', '", _httpListener.Prefixes)}'");

            while (!ct.IsCancellationRequested) {
                await ProcessRequest(ct);
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

    private async Task ProcessRequest(CancellationToken ct) {
        var context = await _httpListener.GetContextAsync();
        if (context.Request.IsWebSocketRequest) {
            await ProcessWebSocketRequest(context, ct);
        }
        else {
            context.Response.StatusCode = 400;
            context.Response.Close();
        }
    }

    private async Task ProcessWebSocketRequest(HttpListenerContext httpContext, CancellationToken ct) {
        var webSocketContext = await httpContext.AcceptWebSocketAsync(subProtocol: null);
        using var webSocket = webSocketContext.WebSocket;

        var buffer = new byte[_bufferSize];
        var result = await webSocket.ReceiveAsync(new(buffer), ct);
        while (!result.CloseStatus.HasValue) {
            await ProcessMessageChunk(buffer.AsSpan()[..result.Count], webSocket, ct);
            result = await webSocket.ReceiveAsync(new(buffer), ct);
        }

        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, ct);
    }

    private Task ProcessMessageChunk(Span<byte> buffer, WebSocket webSocket, CancellationToken ct) {

        var message = UTF8.GetString(buffer.ToArray()).Trim().ToLower();
        switch (message) {
            case "start":
                return _streamer.Start(webSocket, ct);
            case "stop":
                _streamer.Stop();
                return Task.CompletedTask;
            default:
                _logger.LogWarning("Unrecognized command: {command}", message);
                return Task.CompletedTask;
        }
    }
}

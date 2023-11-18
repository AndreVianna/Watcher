namespace Watcher.Daemon.Services;

public class TextStreamer : Streamer<TextStreamer> {
    protected TextStreamer(ILogger<TextStreamer> logger)
        : base(logger) {
    }

    private static readonly byte[] _message = "."u8.ToArray();

    protected override async Task ProcessData(WebSocket webSocket, CancellationToken ct) {
        await webSocket.SendAsync(_message, WebSocketMessageType.Text, false, ct);
        await Task.Delay(100, ct);
    }
}

namespace Watcher.Daemon.Services;

public interface IStreamer : IDisposable {
    Task Start(WebSocket webSocket, CancellationToken ct);
    void Stop();
}
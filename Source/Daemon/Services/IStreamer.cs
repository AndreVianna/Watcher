namespace Watcher.Daemon.Services;

public interface IStreamer : IAsyncDisposable {
    Task Start(CancellationToken ct);
    void Stop();
}

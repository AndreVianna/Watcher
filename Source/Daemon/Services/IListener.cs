namespace Watcher.Daemon.Services;

public interface IListener : IAsyncDisposable {
    Task Start(CancellationToken ct);
    Task Stop(CancellationToken ct = default);

    event ReceivedDataHandler? OnDataReceived;
}

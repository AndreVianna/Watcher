namespace Watcher.WorkstationRegistration.ValueObjects;

public interface IRemoteConnection : IAsyncDisposable {
    Task Connect(CancellationToken ct);
    Task SendCommand(string command, CancellationToken ct);
    Task Disconnect(string reason, CancellationToken ct);
}
namespace Watcher.Common.ValueObjects;

public interface IRemoteConnection : IAsyncDisposable {
    Task Connect(CancellationToken ct);
    Task SendCommand(string command, CancellationToken ct);
    Task SendData(ArraySegment<byte> data, bool isEndOfData, CancellationToken ct);

    event ReceivedDataHandler? OnDataReceived;

    Task Disconnect(string reason, CancellationToken ct);
}

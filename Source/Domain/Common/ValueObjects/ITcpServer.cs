namespace Watcher.Common.ValueObjects;

public interface ITcpServer : IDisposable {
    bool IsListening { get; }
    Task StartListening(string localAddress, bool stopServerOnError, CancellationToken ct);
    void StopListening();

    Task SendData(string remoteAddress, byte[] data, bool stopServerOnError, CancellationToken ct);

    bool IsStreaming { get; }
    Task StartStreaming(string remoteAddress, Func<CancellationToken, Task<DataPackage>> getData, bool stopOnError, CancellationToken ct);
    void StopStreaming();

    event Action? OnServerStated;
    event AsyncEventHandler? OnListeningStarted;
    event AsyncEventHandler<string>? OnClientConnected;
    event AsyncEventHandler<string>? OnStreamingStarted;
    event AsyncEventHandler<(string, DataPackage)>? OnDataReceived;
    event AsyncEventHandler<(string, DataPackage)>? OnDataSent;
    event AsyncEventHandler<(string, DataPackage)>? OnDataStreamed;
    event Action? OnStreamingStopped;
    event Action? OnListeningStopped;
    event Action? OnServerStopped;
}

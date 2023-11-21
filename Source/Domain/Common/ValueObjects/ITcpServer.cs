namespace Watcher.Common.ValueObjects;

public interface ITcpServer : IDisposable {
    bool IsListening { get; }
    Task StartListening(string localAddress, CancellationToken ct);
    void StopListening();

    Task Send<TData>(string remoteAddress, TData data, CancellationToken ct)
        where TData : notnull;
    Task<TResponse> Request<TRequest, TResponse>(string remoteAddress, TRequest request, CancellationToken ct)
        where TRequest : notnull;

    bool IsStreaming { get; }
    Task StartStreaming(string remoteAddress, Func<CancellationToken, Task<DataBlock>> getData, CancellationToken ct);
    void StopStreaming();

    event Action? OnServerStated;
    event AsyncEventHandler? OnListeningStarted;
    event AsyncEventHandler<string>? OnClientConnected;
    event AsyncEventHandler<string>? OnStreamingStarted;
    event AsyncEventHandler<(string, DataBlock?)>? OnDataReceived;
    event AsyncEventHandler<(string, DataBlock)>? OnDataStreamed;
    event AsyncEventHandler<(string, object)>? OnDataSent;
    event AsyncEventHandler<(string, object)>? OnRequestSent;
    event AsyncEventHandler<(string, object?)>? OnResponseReceived;
    event Action? OnStreamingStopped;
    event Action? OnListeningStopped;
    event Action? OnServerStopped;
}

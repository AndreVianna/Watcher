namespace Watcher.Common.ValueObjects;

public interface IRemoteDataServer : IDisposable {
    bool IsRunning { get; }
    // Start and stop the server, handling both TCP and UDP based on port numbers
    void StartListening();
    void StopListening();

    // Send data method. For UDP, using a fire-and-forget
    Task Send<TData>(IPEndPoint remoteAddress, TData data, CancellationToken ct)
        where TData : notnull;
    // Request data method. For TCP, this will be a reliable send and expect a response fom the connected client;
    Task<TResponse?> Request<TRequest, TResponse>(IPEndPoint remoteAddress, TRequest request, CancellationToken ct)
        where TRequest : notnull;

    // Unified streaming method with a parameter to specify streaming type
    bool IsStreaming { get; }
    void StartStreaming<TContent>(IPEndPoint remoteAddress, StreamType streamType, Func<CancellationToken, Task<StreamData<TContent>>> getData);
    void StopStreaming();

    // Events
    event AsyncEventHandler<ServerEventArgs>? OnServerStarted;
    event AsyncEventHandler<ClientEventArgs>? OnClientConnected; // Happens only for TCP
    event AsyncEventHandler<StreamEventArgs>? OnStreamingStarted; // Happens for both TCP and UDP
    event AsyncEventHandler<StreamEventArgs>? OnDataChunkSent; // Happens for both TCP and UDP dIPEndPointng streaming, where object is TContent
    event AsyncEventHandler<StreamEventArgs>? OnDataChunkReceived; // Happens for both TCP and UDP dIPEndPointng streaming, where object is TContent
    event AsyncEventHandler<MessageEventArgs>? OnMessageSent; // Happens only for UDP, object will be of type TData
    event AsyncEventHandler<MessageEventArgs>? OnRequestSent; // Happens only for TCP, object will be of type TRequest
    event AsyncEventHandler<MessageEventArgs>? OnResponseReceived; // Happens only for TCP, object will be of type TResponse
    event AsyncEventHandler? OnStreamingStopped; // Happens for both TCP and UDP
    event AsyncEventHandler<ClientEventArgs>? OnClientDisconnected; // Happens only for TCP
    event AsyncEventHandler? OnServerStopped;
}

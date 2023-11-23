namespace Watcher.Common.ValueObjects;

public record ClientDetails {
    public required IPEndPoint AssuredEndPoint { get; init; }
}

public record RemoteDataServerDetails {
    public required IPEndPoint AssuredEndPoint { get; init; }
    public required IPEndPoint FastEndPoint { get; init; }
}

public record StreamDetails {
    public required StreamType Type { get; init; }
    public required IPEndPoint EndPoint { get; init; }
    public required object? Content { get; init; }
    public required bool IsEndOfData { get; init; }
}

public record MessageDetails {
    public required IPEndPoint EndPoint { get; init; }
    public required object? Content { get; init; }
}

public interface IRemoteDataServer : IAsyncDisposable {
    bool IsRunning { get; }
    // Start and stop the server, handling both TCP and UDP based on port numbers
    Task Start(CancellationToken ct);
    void Stop();

    // Send data method. For UDP, using a fire-and-forget
    Task Send<TData>(IPEndPoint remoteAddress, TData data, bool stopServerOnError, CancellationToken ct)
        where TData : notnull;
    // Request data method. For TCP, this will be a reliable send and expect a response fom the connected client;
    Task<TResponse> Request<TRequest, TResponse>(IPEndPoint remoteAddress, TRequest request, bool stopServerOnError, CancellationToken ct)
        where TRequest : notnull;

    // Unified streaming method with a parameter to specify streaming type
    bool IsStreaming { get; }
    Task StartStreaming<TContent>(IPEndPoint remoteAddress, StreamType streamType, Func<CancellationToken, Task<StreamData<TContent>>> getData, bool stopServerOnError, CancellationToken ct);
    void StopStreaming();

    // Events
    event AsyncEventHandler<RemoteDataServerDetails>? OnServerStarted;
    event AsyncEventHandler<ClientDetails>? OnClientConnected; // Happens only for TCP
    event AsyncEventHandler<StreamDetails>? OnStreamingStarted; // Happens for both TCP and UDP
    event AsyncEventHandler<StreamDetails>? OnDataChunkSent; // Happens for both TCP and UDP dIPEndPointng streaming, where object is TContent
    event AsyncEventHandler<StreamDetails>? OnDataChunkReceived; // Happens for both TCP and UDP dIPEndPointng streaming, where object is TContent
    event AsyncEventHandler<MessageDetails>? OnDataSent; // Happens only for UDP, object will be of type TData
    event AsyncEventHandler<MessageDetails>? OnRequestSent; // Happens only for TCP, object will be of type TRequest
    event AsyncEventHandler<MessageDetails>? OnResponseReceived; // Happens only for TCP, object will be of type TResponse
    event AsyncEventHandler? OnStreamingStopped; // Happens for both TCP and UDP
    event AsyncEventHandler<ClientDetails>? OnClientDisconnected; // Happens only for TCP
    event AsyncEventHandler? OnServerStopped;
}

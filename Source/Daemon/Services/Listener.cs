namespace Watcher.Daemon.Services;

public class Listener : IListener {
    private readonly IRemoteConnection _remoteConnection;

    private bool _isDisposed;

    public event ReceivedDataHandler? OnDataReceived;

    public Listener(IConfiguration configuration, ILoggerFactory loggerFactory) {
        var baseAddress = IsNotNullOrWhiteSpace(configuration.GetValue<string>("Hub:BaseAddress"));
        _remoteConnection = new RemoteConnection(baseAddress, loggerFactory);
    }

    protected virtual Task DisposeAsync(bool disposing) {
        if (!disposing) return Task.CompletedTask;
        OnDataReceived = null;
        return Stop();
    }

    public async ValueTask DisposeAsync() {
        if (_isDisposed) return;
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
        _isDisposed = true;
    }

    public Task Start(CancellationToken ct) {
        _remoteConnection.OnDataReceived += OnDataReceived;
        return _remoteConnection.Connect(ct);
    }

    public Task Stop(CancellationToken ct = default)
        => _remoteConnection.Disconnect("Stop", ct);
}

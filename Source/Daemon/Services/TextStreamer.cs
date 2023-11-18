namespace Watcher.Daemon.Services;

public class TextStreamer : Streamer<TextStreamer> {
    public TextStreamer(IConfiguration configuration, ILoggerFactory loggerFactory)
        : base(configuration, loggerFactory) {
    }

    private static readonly byte[] _message = "."u8.ToArray();

    protected override async Task<DataPackage> GetData(CancellationToken ct) {
        await Task.Delay(100, ct);
        return new() {
            Bytes = new(_message),
            IsEndOfData = false,
        };
    }
}

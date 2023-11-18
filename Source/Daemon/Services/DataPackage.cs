namespace Watcher.Daemon.Services;

public record DataPackage {
    public required ArraySegment<byte> Bytes { get; init; }
    public bool IsEndOfData { get; init; } = true;
}

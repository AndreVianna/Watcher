namespace Watcher.Common.ValueObjects;

public record DataPackage {
    public required byte[] Bytes { get; init; }
    public bool IsEndOfData { get; init; } = true;
}

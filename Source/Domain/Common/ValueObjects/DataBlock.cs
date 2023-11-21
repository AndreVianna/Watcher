namespace Watcher.Common.ValueObjects;

public record DataBlock {
    public required byte[] Bytes { get; init; }
    public bool IsEndOfData { get; init; } = true;
}

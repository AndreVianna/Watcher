namespace Watcher.Common.ValueObjects;

public record StreamEventArgs {
    public required StreamType Type { get; init; }
    public required IPEndPoint EndPoint { get; init; }
    public required object? Content { get; init; }
    public required bool IsEndOfData { get; init; }
}
namespace Watcher.Common.ValueObjects;

public record MessageEventArgs {
    public required IPEndPoint EndPoint { get; init; }
    public required object? Content { get; init; }
}

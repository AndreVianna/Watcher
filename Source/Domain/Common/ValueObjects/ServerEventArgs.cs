namespace Watcher.Common.ValueObjects;

public record ServerEventArgs {
    public required IPEndPoint AssuredEndPoint { get; init; }
    public required IPEndPoint FastEndPoint { get; init; }
}

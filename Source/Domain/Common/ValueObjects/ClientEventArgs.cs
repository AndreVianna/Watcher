namespace Watcher.Common.ValueObjects;

public record ClientEventArgs {
    public required IPEndPoint AssuredEndPoint { get; init; }
}
namespace Watcher.Common.ValueObjects;

public record StreamData<TContent> {
    public required TContent Content { get; init; }
    public bool IsEndOfData { get; init; } = true;
}

namespace Watcher.Caller.Commands;

public record ApplicationDto {
    public required string Name { get; set; }
    public string? Arguments { get; set; }
}

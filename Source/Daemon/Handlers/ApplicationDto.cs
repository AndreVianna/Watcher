namespace Watcher.Daemon.Handlers;

public record ApplicationDto {
    public required string Name { get; set; }
    public string? Arguments { get; set; }
}

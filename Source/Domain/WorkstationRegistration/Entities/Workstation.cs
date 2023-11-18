namespace Watcher.WorkstationRegistration.Entities;
internal record Workstation : IEntity {
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Address { get; init; }
}

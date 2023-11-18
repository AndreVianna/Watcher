using Watcher.WorkstationRegistration.ValueObjects;

namespace Watcher.WorkstationRegistration.Entities;

public record Workstation : IEntity, IWorkstation {
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Address { get; init; }

    public IRemoteConnection? OpenConnection(ILoggerFactory loggerFactory)
        => Address is null ? null : new RemoteConnection(Address, loggerFactory);
}

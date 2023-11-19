using Watcher.Common.ValueObjects;

namespace Watcher.WorkstationRegistration.Entities;

public record Workstation : IEntity, IWorkstation {
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Address { get; init; }

    public ITcpServer CreateServer(ILoggerFactory loggerFactory)
        => new TcpServer(loggerFactory);
}

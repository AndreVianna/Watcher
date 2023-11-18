using Watcher.WorkstationRegistration.ValueObjects;

namespace Watcher.WorkstationRegistration.Entities;

public interface IWorkstation {
    string? Address { get; init; }
    string Id { get; init; }
    string Name { get; init; }

    IRemoteConnection? OpenConnection(ILoggerFactory loggerFactory);
}

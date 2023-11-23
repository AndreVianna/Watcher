namespace Watcher.WorkstationRegistration.Entities;

public record Workstation : IEntity, IWorkstation {
    public required string Id { get; init; }
    public required string Name { get; init; }
    public IPEndPoint? Address { get; init; }

    public IRemoteDataServer CreateServer(IConfiguration configuration, ILoggerFactory loggerFactory)
        => new RemoteDataServer(configuration, loggerFactory);
}

namespace Watcher.WorkstationRegistration.Entities;

public interface IWorkstation {
    IPEndPoint? Address { get; init; }
    string Id { get; init; }
    string Name { get; init; }

    IRemoteDataServer CreateServer(IConfiguration configuration, ILoggerFactory loggerFactory);
}

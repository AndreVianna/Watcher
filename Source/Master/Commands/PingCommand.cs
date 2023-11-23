using System.Net;

using static DotNetToolbox.Ensure;

namespace Watcher.Master.Commands;

internal class PingCommand : Command<PingCommand> {
    private readonly ServiceProvider _services;

    public PingCommand(ServiceProvider services)
        : base("Ping", "Send a ping to the watcher process.") {
        _services = services;
        OnExecute += (_, _) => Execute();
    }

    private Task Execute() {
        var configuration = _services.GetRequiredService<IConfiguration>();
        var loggerFactory = _services.GetService<ILoggerFactory>();
        var server = new RemoteDataServer(configuration, _services.GetRequiredService<ILoggerFactory>());
        var address = IsNotNull(configuration["Watcher:Address"]);
        var port = Convert.ToInt32(IsNotNull(configuration["Watcher:Port"]));
        var endPoint = new IPEndPoint(IPAddress.Parse(address), port);
        return server.Send(endPoint, "Ping"u8.ToArray(), default);
    }
}

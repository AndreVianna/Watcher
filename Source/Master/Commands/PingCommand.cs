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
        var server = new TcpServer(_services.GetRequiredService<ILoggerFactory>());
        var configuration = _services.GetRequiredService<IConfiguration>();
        var address = IsNotNull(configuration["Watcher:BaseAddress"]);
        return server.SendData(address, "Ping"u8.ToArray(), false, default);
    }
}

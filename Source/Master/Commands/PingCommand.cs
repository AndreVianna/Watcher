using Watcher.Common.ValueObjects;

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
        return server.SendData("127.0.0.1:5000", "Ping"u8.ToArray(), false, default);
    }
}

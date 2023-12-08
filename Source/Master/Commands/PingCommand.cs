namespace Watcher.Caller.Commands;

internal class PingCommand : Command<PingCommand> {
    private readonly ClientWebSocket _tcpClient;

    public PingCommand(IServiceProvider provider)
        : base("Ping", "Send a ping to the watcher process.") {
        _tcpClient = provider.GetRequiredService<ClientWebSocket>();
        OnExecute += (args, _) => Ping(args[0]);
    }

    private async Task Ping(string endPoint) {
        Writer.WriteLine($"Ping {endPoint}!");
        var request = new Request { Verb = "Command", Route = "Ping", };
        var response = await RemoteAccess.Request<string>(_tcpClient, endPoint, request, CancellationToken.None);
        Writer.WriteLine(response);
    }
}

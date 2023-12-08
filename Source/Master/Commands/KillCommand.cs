namespace Watcher.Caller.Commands;

internal class KillCommand : Command<KillCommand> {
    private readonly ClientWebSocket _tcpClient;

    public KillCommand(IServiceProvider provider)
        : base("Kill", "Send kill by name request.") {
        _tcpClient = provider.GetRequiredService<ClientWebSocket>();
        OnExecute += (args, _) => KillByName(args[0], args[1]);
    }

    private async Task KillByName(string endPoint, string id) {
        Writer.WriteLine("Starting...");
        var request = new Request { Verb = "Command", Route = "Kill", Content = id, };
        var response = await RemoteAccess.Request<string>(_tcpClient, endPoint, request, CancellationToken.None);
        Writer.WriteLine(response);
    }
}

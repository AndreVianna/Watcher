namespace Watcher.Caller.Commands;

internal class StopCommand : Command<StopCommand> {
    private readonly ClientWebSocket _tcpClient;

    public StopCommand(IServiceProvider provider)
        : base("Stop", "Send Stop request.") {
        _tcpClient = provider.GetRequiredService<ClientWebSocket>();
        OnExecute += (args, _) => Stop(args[0], args[1]);
    }

    private async Task Stop(string endPoint, string name) {
        Writer.WriteLine("Starting...");
        var request = new Request { Verb = "Command", Route = "Stop", Content = name, };
        var response = await RemoteAccess.Request<string>(_tcpClient, endPoint, request, CancellationToken.None);
        Writer.WriteLine(response);
    }
}

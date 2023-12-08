namespace Watcher.Caller.Commands;

internal class StartCommand : Command<StartCommand> {
    private readonly ClientWebSocket _tcpClient;

    public StartCommand(IServiceProvider provider)
        : base("Start", "Send Start request.") {
        _tcpClient = provider.GetRequiredService<ClientWebSocket>();
        OnExecute += (args, _) => Start(args[0], args[1]);
    }

    private async Task Start(string endPoint, string name) {
        Writer.WriteLine("Starting...");
        var request = new Request { Verb = "Command", Route = "Start", Content = name, };
        var response = await RemoteAccess.Request<string>(_tcpClient, endPoint, request, CancellationToken.None);
        Writer.WriteLine(response);
    }
}

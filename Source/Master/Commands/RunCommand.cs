namespace Watcher.Caller.Commands;

internal class RunCommand : Command<RunCommand> {
    private readonly ClientWebSocket _tcpClient;

    public RunCommand(IServiceProvider provider)
        : base("Run", "Send Run request.") {
        _tcpClient = provider.GetRequiredService<ClientWebSocket>();
        OnExecute += (args, _) => {
            var arguments = args.Length > 2 ? string.Join(' ', args[2..]) : null;
            return Run(args[0], args[1], arguments);
        };
    }

    private async Task Run(string endPoint, string name, string? args) {
        Writer.WriteLine("Starting...");
        var content = new ApplicationDto {
            Name = name,
            Arguments = args,
        };
        var request = new Request { Verb = "Command", Route = "Run", Content = content, };
        var response = await RemoteAccess.Request<string>(_tcpClient, endPoint, request, CancellationToken.None);
        Writer.WriteLine(response);
    }
}

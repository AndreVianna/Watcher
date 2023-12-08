namespace Watcher.Caller.Commands;

internal class MainCommand : RootCommand<MainCommand> {
    public MainCommand(IServiceProvider services) {
        Add(new PingCommand(services));
        Add(new StartCommand(services));
        Add(new StopCommand(services));
        Add(new RunCommand(services));
        Add(new KillCommand(services));
    }
}

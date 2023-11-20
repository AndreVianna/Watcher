namespace Watcher.Master.Commands;

internal class MainCommand : RootCommand<MainCommand> {
    public MainCommand(ServiceProvider services) {
        Add(new PingCommand(services));
    }
}

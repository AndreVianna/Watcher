namespace Watcher.Hub.Commands;

internal class MainCommand : RootCommand<MainCommand> {
    public MainCommand() {
        Add(new ListCommand());
        Add(new StartCommand());
        Add(new StopCommand());
    }
}

namespace Watcher.Daemon.Commands;

internal class MainCommand : RootCommand {
    public MainCommand() {
        Add(new StartCommand());
        Add(new StopCommand());
    }
}

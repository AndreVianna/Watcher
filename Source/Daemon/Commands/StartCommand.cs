namespace Watcher.Daemon.Commands;

internal class StartCommand : SubCommand {
    public StartCommand()
        : base("Start", "Start a process in the local machine.") {
        Add(new StartCastingCommand());
        Add(new StartRecordingCommand());
    }
}

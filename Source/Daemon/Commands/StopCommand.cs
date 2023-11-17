namespace Watcher.Daemon.Commands;

internal class StopCommand : SubCommand {
    public StopCommand()
        : base("Stop", "Stop a running process.") {
        Add(new StopCastingCommand());
        Add(new StopRecordingCommand());
    }
}

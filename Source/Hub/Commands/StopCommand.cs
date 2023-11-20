namespace Watcher.Hub.Commands;

internal class StopCommand : Command<StopCommand> {
    public StopCommand()
        : base("Stop", "Stop remote streaming process.") {
    }

    // ToDo: Send command to Daemon to stop a streaming.
}

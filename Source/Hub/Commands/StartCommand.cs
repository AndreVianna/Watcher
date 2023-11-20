namespace Watcher.Hub.Commands;

internal class StartCommand : Command<StartCommand> {
    public StartCommand()
        : base("Start", "Start remote streaming process.") {
    }

    // ToDo: Send command to Daemon to start a streaming.
}

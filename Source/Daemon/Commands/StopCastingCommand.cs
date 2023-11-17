namespace Watcher.Daemon.Commands;

internal class StopCastingCommand : SubCommand {
    public StopCastingCommand()
        : base("Casting",
            "Stops the screen casting process if it is running.",
            onExecute: cmd => cmd.Writer.WriteLine("Casting stopped.")) {
    }
}

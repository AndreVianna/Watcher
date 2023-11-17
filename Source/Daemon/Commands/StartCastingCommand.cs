namespace Watcher.Daemon.Commands;

internal class StartCastingCommand : SubCommand {
    public StartCastingCommand()
        : base("Casting",
            "Start the screen casting process.",
            onExecute: cmd => cmd.Writer.WriteLine("Casting started.")) {
    }
}

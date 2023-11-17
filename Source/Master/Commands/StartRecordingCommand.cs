namespace Watcher.Master.Commands;

internal class StartRecordingCommand : SubCommand {
    public StartRecordingCommand()
        : base("Recording",
            "Start the local screen recording process.",
            onExecute: cmd => cmd.Writer.WriteLine("Recording started.")) {
    }
}

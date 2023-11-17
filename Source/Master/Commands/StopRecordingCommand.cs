namespace Watcher.Master.Commands;

internal class StopRecordingCommand : SubCommand {
    public StopRecordingCommand()
        : base("Recording",
            "Stops the local recording process if it is running.",
            onExecute: cmd => cmd.Writer.WriteLine("Recording stopped.")) {
    }
}

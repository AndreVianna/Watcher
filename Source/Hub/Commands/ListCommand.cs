namespace Watcher.Hub.Commands;

internal class ListCommand : SubCommand {
    public ListCommand()
        : base("List", "List the workstations connected via the watcher.") {
    }

    // ToDo: List the workstations with the connection status.
}

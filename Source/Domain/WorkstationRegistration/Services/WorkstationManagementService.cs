namespace Watcher.WorkstationRegistration.Services;

public class WorkstationManagementService
    : IWorkstationManagementService {

    private readonly List<Workstation> _workstations = new() {
        new() {
            Id = "86324bdd-5787-4a8e-a817-878f69d643ad",
            Name = "Daemon1",
            Address = "ws://localhost:5001",
        },
        new() {
            Id = "7b9974e7-7e69-4e37-8be6-f1b22e44a733",
            Name = "Daemon2",
            Address = "ws://localhost:5002",
        },
    };

    public IEnumerable<IWorkstation> GetAll() => _workstations.Cast<IWorkstation>();
}

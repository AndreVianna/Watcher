
namespace Watcher.WorkstationRegistration.Services;
public interface IWorkstationManagementService {
    IEnumerable<IWorkstation> GetAll();
}

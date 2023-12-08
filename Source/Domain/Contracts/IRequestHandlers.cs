namespace DotnetToolbox.Net.Contracts;

public interface IRequestHandlers {
    IRequestHandler? GetValueOrDefault(int id);
}

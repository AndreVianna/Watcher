namespace DotnetToolbox.Net.Contracts;

public interface IDataExchangeAgent : IDisposable {
    bool IsRunning { get; }
    Connection ToConnection { get; }

    void Start();
    void Stop();

    string AddConnection(Connection connection);
    IEnumerable<Connection> GetConnections();
    void RemoveConnection(string id);

    Task<Response> SendRequestTo(string connectionId, Request request, CancellationToken ct);
}

namespace DotnetToolbox.Net.DataExchange;

internal sealed record Agent {
    [SetsRequiredMembers]
    public Agent(string id, string endPoint)
        : this(id, IPEndPoint.Parse(endPoint)) {
    }

    [SetsRequiredMembers]
    private Agent(string id, IPEndPoint endPoint) {
        Id = IsNotNull(id);
        EndPoint = endPoint;
        Listener = new HttpListener();
        Listener.Prefixes.Add($"http://{endPoint}/");
    }

    [SetsRequiredMembers]
    public Agent(Connection connection)
        : this(connection.Id, IPEndPoint.Parse(connection.EndPoint)) {
    }

    public string Id { get; }
    public Connection ToConnection() => new(Id, EndPoint.ToString());

    internal IPEndPoint EndPoint { get; }
    internal HttpListener Listener { get; private init; }
    internal Dictionary<string, Agent> Connections { get; } = new();
}

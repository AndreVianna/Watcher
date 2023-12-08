namespace DotnetToolbox.Net.Contracts;

public sealed record Connection() {
    [SetsRequiredMembers]
    public Connection(string id, string endPoint)
        : this() {
        Id = id;
        EndPoint = endPoint;
    }

    public required string Id { get; init; }
    public required string EndPoint { get; init; }
}

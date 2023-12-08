namespace DotnetToolbox.Net.Contracts;

public sealed class DataExchangeAgentOptions : IOptions {
    public static string SectionName => nameof(DataExchangeAgent);

    public string? Id { get; set; }
    public string? Endpoint { get; set; }
    public int? BufferSize { get; set; }

    public Connection[] Connections { get; set; } = Array.Empty<Connection>();

}

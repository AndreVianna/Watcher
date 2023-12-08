namespace DotnetToolbox.Net.Contracts;

public record RequestResponse {
    public required string RequestId { get; init; }
    public required Response Response { get; init; }

    public int StatusCode => Response.StatusCode;

    public TContent? GetResponseContentOrDefault<TContent>()
        => Response.Content switch {
               TContent value => value,
               JsonElement element => element.Deserialize<TContent>(),
               string json => JsonSerializer.Deserialize<TContent>(json),
               byte[] bytes => JsonSerializer.Deserialize<TContent>(Encoding.UTF8.GetString(bytes)),
               _ => default,
           };
}

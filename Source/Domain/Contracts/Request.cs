namespace DotnetToolbox.Net.Contracts;

public record Request {
    private readonly string _verb = string.Empty;
    private readonly string _route = string.Empty;

    public string Id { get; init; } = Guid.NewGuid().ToString();

    public required string Route {
        get => _route;
        init => _route = value.ToLower();
    }

    public required string Verb {
        get => _verb;
        init => _verb = value.ToUpper();
    }

    public object? Content { get; init; }

    public TContent? GetContentOrDefault<TContent>() {
        try {
            return Content switch {
                       TContent value => value,
                       JsonElement element => element.Deserialize<TContent>(),
                       string json => JsonSerializer.Deserialize<TContent>(json),
                       byte[] bytes => JsonSerializer.Deserialize<TContent>(Encoding.UTF8.GetString(bytes)),
                       _ => default,
                   };
        }
        catch {
            return default;
        }
    }
}

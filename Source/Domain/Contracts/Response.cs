namespace DotnetToolbox.Net.Contracts;

public record Response {
    public static Response Default()
        => new() {
            Content = default!,
            StatusCode = 200,
        };

    public required int StatusCode { get; init; }
    public object? Content { get; init; }

    public static Response BadRequest(string errorMessage)
        => new() {
            StatusCode = (int)HttpStatusCode.BadRequest,
            Content = errorMessage,
        };

    public static Response NotFound()
        => new() {
            StatusCode = (int)HttpStatusCode.NotFound,
            Content = default!,
        };

    public static Response InternalServerError(string message, Exception? exception = default)
        => new() {
            StatusCode = (int)HttpStatusCode.InternalServerError,
            Content = new Exception(message, exception),
        };

    public static Response Ok(object? content = null)
        => new() {
            StatusCode = (int)HttpStatusCode.OK,
            Content = content,
        };

    public TContent? GetContentOrDefault<TContent>()
        => Content switch {
            TContent value => value,
            JsonElement element => element.Deserialize<TContent>(),
            string json => JsonSerializer.Deserialize<TContent>(json),
            byte[] bytes => JsonSerializer.Deserialize<TContent>(Encoding.UTF8.GetString(bytes)),
            _ => default,
        };
}

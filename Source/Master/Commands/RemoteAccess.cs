namespace Watcher.Caller.Commands;

internal class RemoteAccess
{
    internal static async Task<string> Request<TResponse>(ClientWebSocket tcpClient, string port, Request request, CancellationToken ct)
    {
        await tcpClient.ConnectAsync(new Uri($"ws://172.21.176.1:{port}"), ct).ConfigureAwait(false);
        try {
            var requestJson = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
            await tcpClient.SendAsync(requestJson.AsMemory(),
                                      WebSocketMessageType.Binary,
                                      true,
                                      ct)
                           .ConfigureAwait(false);
            var message = await ReadResponse(tcpClient, ct);
            return $"{message.StatusCode}: {message.GetResponseContentOrDefault<TResponse>()!}";
        }
        finally {
            await tcpClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close", ct);
        }
    }

    internal static async Task<RequestResponse> ReadResponse(WebSocket tcpClient, CancellationToken ct) {
        await using var memoryStream = new MemoryStream();
        var isEndOfMessage = false;
        while (!isEndOfMessage) {
            var buffer = new byte[1024];
            var result = await tcpClient.ReceiveAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            await memoryStream.WriteAsync(buffer[..result.Count].AsMemory(), ct);
            isEndOfMessage = result.EndOfMessage;
        }
        var json = Encoding.UTF8.GetString(memoryStream.ToArray());
        var content = JsonSerializer.Deserialize<RequestResponse>(json);
        return IsNotNull(content);
    }
}

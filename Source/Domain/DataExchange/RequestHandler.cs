namespace DotnetToolbox.Net.DataExchange;

internal sealed record RequestHandler<TRequest, TResponse> : IRequestHandler
    where TRequest : Request
    where TResponse : Response {
    private readonly Func<TRequest, CancellationTokenSource, Task<TResponse>> _handler;

    public RequestHandler(Func<TRequest, CancellationTokenSource, Task<TResponse>> handler) {
        _handler = handler;
    }

    public Task<TResponse> Handle(TRequest request, CancellationTokenSource cts)
        => _handler(request, cts);

    async Task<Response> IRequestHandler.Handle(Request request, CancellationTokenSource cts)
        => await Handle((TRequest)request, cts);
}

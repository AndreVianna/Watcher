namespace DotnetToolbox.Net.Contracts;

public interface IRequestHandler {
    public static int CreateId(string route, Type requestType)
        => HashCode.Combine(route.GetHashCode(), requestType.GetHashCode());

    Task<Response> Handle(Request request, CancellationTokenSource cts);
}

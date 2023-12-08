namespace DotnetToolbox.Net.DataExchange;

internal sealed class RequestHandlers<TReference> : IRequestHandlers {

    private readonly Dictionary<int, IRequestHandler> _handlers = new();

    public RequestHandlers(IServiceProvider services) {
        var assembly = typeof(TReference).Assembly;
        var handlers = assembly.GetTypes()
                                  .Where(i => i.IsDefined(typeof(HandlerAttribute)))
                                  .Select(i => (i, i.GetCustomAttribute<HandlerAttribute>()!.BaseRoute));
        foreach ((var handlerType, var baseRoute) in handlers) {
            RegisterHandlerActions(services, handlerType, baseRoute);
        }
    }

    public IRequestHandler? GetValueOrDefault(int id) => _handlers.GetValueOrDefault(id);

    private void RegisterHandlerActions(IServiceProvider services, Type handlerType, string baseRoute) {
        var handlers = handlerType
                      .GetMethods()
                      .Where(i => i.IsDefined(typeof(RouteAttribute)))
                      .Select(i => (i, Route: DefineRoute(i)));
        foreach ((var method, var route) in handlers)
            RegisterHandler(services, handlerType, route, method);

        return;

        string DefineRoute(MemberInfo i)
            => (string.IsNullOrWhiteSpace(baseRoute)
                   ? string.Empty
                   : $"{baseRoute}/") + i.GetCustomAttribute<RouteAttribute>()!.Route;
    }

    private void RegisterHandler(IServiceProvider services, Type handlerType, string route, MethodInfo method) {
        var returnType = GetReturnTypeName(method);
        var isStatic = method.IsStatic;
        var arguments = method.GetParameters();
        var firstArgType = GetArgumentName(arguments, 0);
        var secondArgType = GetArgumentName(arguments, 1);
        Func<Request, CancellationTokenSource, Task<Response>> handler = (isStatic, returnType, firstArgType, secondArgType) switch {
            (false, "Task", "Request", "Token") =>
                (r, cts) => (Task<Response>)method
                                           .CreateDelegate<Func<Request, CancellationTokenSource, Task<Response>>>(services.GetRequiredService(handlerType))
                                           .DynamicInvoke(r, cts)!,
            (false, "Task", "Request", null) =>
                (r, _) => (Task<Response>)method
                                         .CreateDelegate<Func<Request, Task<Response>>>(services.GetRequiredService(handlerType))
                                         .DynamicInvoke(r)!,
            (false, "Task", "Token", null) =>
                (_, cts) => (Task<Response>)method
                                           .CreateDelegate<Func<CancellationTokenSource, Task<Response>>>(services.GetRequiredService(handlerType))
                                           .DynamicInvoke(cts)!,
            (false, "Task", null, null) =>
                (_, _) => (Task<Response>)method.CreateDelegate<Func<Task<Response>>>(services.GetRequiredService(handlerType))
                                                .DynamicInvoke()!,
            (false, "Value", "Request", null) =>
                (r, cts) => Task.Run(() => (Response)method
                                                    .CreateDelegate<Func<Request, Response>>(services.GetRequiredService(handlerType))
                                                    .DynamicInvoke(r)!, cts.Token),
            (false, "Value", null, null) =>
                (_, cts) => Task.Run(() => (Response)method
                                                    .CreateDelegate<Func<Response>>(services.GetRequiredService(handlerType))
                                                    .DynamicInvoke()!, cts.Token),
            (true, "Task", "Request", "Token") =>
                (r, cts) => (Task<Response>)method
                                           .CreateDelegate<Func<Request, CancellationTokenSource, Task<Response>>>()
                                           .DynamicInvoke(r, cts)!,
            (true, "Task", "Request", null) =>
                (r, _) => (Task<Response>)method
                                         .CreateDelegate<Func<Request, Task<Response>>>()
                                         .DynamicInvoke(r)!,
            (true, "Task", "Token", null) =>
                (_, cts) => (Task<Response>)method
                                           .CreateDelegate<Func<CancellationTokenSource, Task<Response>>>()
                                           .DynamicInvoke(cts)!,
            (true, "Task", null, null) =>
                (_, _) => (Task<Response>)method
                                         .CreateDelegate<Func<Task<Response>>>()
                                         .DynamicInvoke()!,
            (true, "Value", "Request", null) =>
                (r, cts) => Task.Run(() => (Response)method
                                                    .CreateDelegate<Func<Request, Response>>()
                                                    .DynamicInvoke(r)!, cts.Token),
            (true, "Value", null, null) =>
                (_, cts) => Task.Run(() => (Response)method
                                                    .CreateDelegate<Func<Response>>()
                                                    .DynamicInvoke()!, cts.Token),
            _ =>
                throw new InvalidCastException("The action signature is not valid."),
        };

        AddRequestHandler(route, handler);
        return;

        static string? GetArgumentName(IReadOnlyList<ParameterInfo> parameterInfos, int i)
            => parameterInfos.Count <= i ? null
             : parameterInfos[i].ParameterType.IsAssignableTo(typeof(Request)) ? "Request"
             : parameterInfos[i].ParameterType.IsAssignableTo(typeof(CancellationTokenSource)) ? "Token"
             : null;

        static string? GetReturnTypeName(MethodInfo methodInfo)
            => methodInfo.ReturnType.IsAssignableTo(typeof(Task<Response>)) ? "Task"
             : methodInfo.ReturnType.IsAssignableTo(typeof(Response)) ? "Value"
             : null;
    }

    private void AddRequestHandler<TRequest, TResponse>(string route, Func<TRequest, CancellationTokenSource, Task<TResponse>> handler)
        where TRequest : Request
        where TResponse : Response {
        var id = IRequestHandler.CreateId(route, typeof(TRequest));
        _handlers.Add(id, new RequestHandler<TRequest, TResponse>(handler));
    }
}

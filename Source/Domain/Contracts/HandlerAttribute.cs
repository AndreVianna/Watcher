namespace DotnetToolbox.Net.Contracts;

[AttributeUsage(AttributeTargets.Class)]
public class HandlerAttribute : Attribute {
    public HandlerAttribute(string? baseRoute = null) {
        BaseRoute = baseRoute ?? string.Empty;
    }

    public string BaseRoute { get; }
}

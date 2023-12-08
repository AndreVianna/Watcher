namespace DotnetToolbox.Net.Contracts;

[AttributeUsage(AttributeTargets.Method)]
public class RouteAttribute : Attribute {
    public RouteAttribute(string route) {
        Route = route;
    }

    public string Route { get; }
}

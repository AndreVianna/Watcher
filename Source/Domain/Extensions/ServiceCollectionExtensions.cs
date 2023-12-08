namespace DotnetToolbox.Net.Extensions;
public static class ServiceCollectionExtensions {
    public static IServiceCollection AddDataExchangeHostedService<TReference>(this IServiceCollection services, IConfiguration configuration, Action<DataExchangeAgentOptions>? configure = null) {
        var optionsBuilder = services.AddOptions<DataExchangeAgentOptions>(DataExchangeAgentOptions.SectionName);
        optionsBuilder.BindConfiguration(DataExchangeAgentOptions.SectionName);
        services.Configure<DataExchangeAgentOptions>(configuration.GetSection(DataExchangeAgentOptions.SectionName));
        if (configure is not null) optionsBuilder.Configure(configure);
        var assembly = typeof(TReference).Assembly;
        var handlers = assembly.GetTypes()
                               .Where(i => i.IsDefined(typeof(HandlerAttribute)))
                               .ToArray();
        foreach (var handlerType in handlers) services.AddSingleton(handlerType);
        services.AddSingleton<IRequestHandlers, RequestHandlers<TReference>>();
        services.AddSingleton<IDataExchangeAgent, DataExchangeAgent>();
        services.AddHostedService<DataExchangeHostedService>();
        return services;
    }
}

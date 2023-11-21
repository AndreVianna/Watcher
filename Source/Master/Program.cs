var environmentName = args.LastOrDefault() == "dev" ? "Development" : Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

var builder = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true);
var configuration = builder.Build();

Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddSingleton<ILoggerFactory, LoggerFactory>();
services.AddSingleton<IWorkstationManagementService, WorkstationManagementService>();
services.AddLogging(conf => conf.AddSerilog(Log.Logger));

var serviceProvider = services.BuildServiceProvider();

var main = new MainCommand(serviceProvider);
await main.Execute(args);

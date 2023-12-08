using Watcher.Caller.Commands;

var builder = new ConfigurationBuilder()
             .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
var configuration = builder.Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddSingleton<ILoggerFactory, LoggerFactory>();
services.AddSingleton<ClientWebSocket>();
services.AddLogging();

var serviceProvider = services.BuildServiceProvider();

var main = new MainCommand(serviceProvider);
await main.Execute(args);

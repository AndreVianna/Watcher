var builder = new ConfigurationBuilder()
   .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var configuration = builder.Build();

var host = Host.CreateDefaultBuilder(args)
               .ConfigureLogging(logger => {
                   logger.ClearProviders();
                   logger.AddConsole();
               })
               .ConfigureServices((_, services) => {
                   services.AddSingleton<IConfiguration>(configuration);
                   services.AddHostedService<WatcherDaemonService>();
               })
               .Build();

await host.RunAsync();

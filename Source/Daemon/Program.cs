Log.Logger = new LoggerConfiguration()
            .WriteTo.File("logs/watcherdaemon.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

var builder = new ConfigurationBuilder()
   .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var configuration = builder.Build();

var host = Host.CreateDefaultBuilder(args)
               .UseSerilog(ConfigLogger)
               .ConfigureServices((_, services) => {
                   services.AddSingleton<IConfiguration>(configuration);
                   services.AddHostedService<WatcherDaemonService>();
               })
               .Build();

await host.RunAsync();
return;

static void ConfigLogger(HostBuilderContext content, LoggerConfiguration logger) {
    if (!content.HostingEnvironment.IsProduction()) {
        logger.WriteTo.Console();
    }
    logger.WriteTo.File("logs/watcher.log");
}

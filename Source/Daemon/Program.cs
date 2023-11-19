var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

var host = Host.CreateDefaultBuilder(args)
               .UseEnvironment(environmentName ?? "Production")
               .ConfigureAppConfiguration((context, config) => {
                   config.AddCommandLine(args);
                   config.AddEnvironmentVariables();
                   config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                   config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                   config.AddUserSecrets<Program>();
               })
               .ConfigureServices((context, services) => {
                   services.AddSingleton(context.Configuration);
                   services.AddSingleton<ITcpServer, TcpServer>();
                   services.AddLogging();
                   services.AddHostedService<WatcherService>();
               })
               .UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration))
               .Build();

var cts = new CancellationTokenSource();
await host.RunAsync(cts.Token);

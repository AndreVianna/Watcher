using Serilog;

var builder = new ConfigurationBuilder()
   .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
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

var wms = serviceProvider.GetRequiredService<IWorkstationManagementService>();
var workstations = wms.GetAll();

var cts = new CancellationTokenSource();

foreach (var workstation in workstations) {
    try {
        // Attempt to connect to the daemon
        if (string.IsNullOrWhiteSpace(workstation.Address)) continue;
        var server = workstation.CreateServer(serviceProvider.GetRequiredService<ILoggerFactory>());
        server.SendData(workstation.Address, "Ping"u8.ToArray(), false, cts.Token);
        Console.WriteLine($"Connected to {workstation.Name} successfully.");
    }
    catch (Exception ex) {
        Console.WriteLine($"Failed to connect to {workstation.Name}: {ex.Message}");
    }
}
MainCommand main = new();

main.Execute(args);

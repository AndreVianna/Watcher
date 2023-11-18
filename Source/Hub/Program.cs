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

foreach (var workstation in workstations) {
    try {
        // Attempt to connect to the daemon
        workstation.OpenConnection(serviceProvider.GetRequiredService<ILoggerFactory>());
        Console.WriteLine($"Connected to {workstation.Name} successfully.");
    }
    catch (Exception ex) {
        Console.WriteLine($"Failed to connect to {workstation.Name}: {ex.Message}");
    }
}
MainCommand main = new();

main.Execute(args);

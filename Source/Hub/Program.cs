var builder = new ConfigurationBuilder()
   .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
var configuration = builder.Build();

var watchers = configuration.GetSection("Watchers").Get<List<WorkstationConfig>>();

foreach (var daemon in watchers) {
    try {
        // Attempt to connect to the daemon
        ConnectToWatcher(daemon);
        Console.WriteLine($"Connected to {daemon.Name} successfully.");
    }
    catch (Exception ex) {
        Console.WriteLine($"Failed to connect to {daemon.Name}: {ex.Message}");
    }
}
MainCommand main = new();

main.Execute(args);

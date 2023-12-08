using DotnetToolbox.Net.Extensions;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureAppConfiguration((config) => {
                                              config.AddCommandLine(args);
                                              config.AddEnvironmentVariables();
                                              config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                                              config.AddUserSecrets<Program>();
                                          })
               .ConfigureServices((context, services) => {
                                      services.AddSingleton(context.Configuration);
                                      services.AddLogging();
                                      services.AddDataExchangeHostedService<Program>(context.Configuration);
                                  })
               .Build();

var cts = new CancellationTokenSource();
await host.RunAsync(cts.Token);

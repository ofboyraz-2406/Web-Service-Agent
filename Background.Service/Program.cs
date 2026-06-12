using Background.service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();

        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
    })
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
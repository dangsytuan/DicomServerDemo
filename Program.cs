using FellowOakDicom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices(services =>
    {
        services.AddFellowOakDicom();
        services.AddTransient<SCPService>();
        services.AddHostedService<DicomListener>();
    })
    .Build();
    
DicomSetupBuilder.UseServiceProvider(host.Services);

host.Run();

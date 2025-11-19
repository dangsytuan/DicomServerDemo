using FellowOakDicom.Network;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class DicomListener : IHostedService
{
    private readonly ILogger<DicomListener> _logger;
    private readonly IDicomServerFactory _factory;
    private IDicomServer? _server;

    public DicomListener(ILogger<DicomListener> logger, IDicomServerFactory factory)
    {
        _logger = logger;
        _factory = factory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🚀 Starting DICOM Server on port 1112");

        // DÙNG SCPService duy nhất
        _server = _factory.Create<SCPService>(1112);

        _logger.LogInformation("📡 DICOM Server is running");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _server?.Stop();
        _server?.Dispose();
        _logger.LogInformation("🛑 DICOM Server stopped");
        return Task.CompletedTask;
    }
}

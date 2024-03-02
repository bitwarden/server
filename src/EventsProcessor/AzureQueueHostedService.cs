namespace Bit.EventsProcessor;

public class AzureQueueHostedService : BackgroundService, IDisposable
{
    private readonly IProcessor _processor;
    private readonly ILogger<AzureQueueHostedService> _logger;
    private readonly IConfiguration _configuration;

    public AzureQueueHostedService(
        IProcessor processor, ILogger<AzureQueueHostedService> logger, IConfiguration configuration)
    {
        _processor = processor;
        _logger = logger;
        _configuration = configuration;
    }

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var storageConnectionString = _configuration["azureStorageConnectionString"];
        if (string.IsNullOrWhiteSpace(storageConnectionString))
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var didProcess = await _processor.ProcessAsync(cancellationToken);
                if (!didProcess)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while processing events queue.");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        _logger.LogWarning("Done processing.");
    }
}

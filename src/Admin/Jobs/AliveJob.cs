using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Admin.Jobs;

public class AliveJob : BaseJob
{
    private readonly GlobalSettings _globalSettings;
    private readonly IHttpClientFactory _httpClientFactory;

    public AliveJob(
        GlobalSettings globalSettings,
        IHttpClientFactory httpClientFactory,
        ILogger<AliveJob> logger)
        : base(logger)
    {
        _globalSettings = globalSettings;
        _httpClientFactory = httpClientFactory;
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: Keep alive");
        var response = await _httpClientFactory.CreateClient().GetAsync(_globalSettings.BaseServiceUri.Admin);
        _logger.LogInformation(Constants.BypassFiltersEventId, "Finished job task: Keep alive, {StatusCode}",
            response.StatusCode);
    }
}

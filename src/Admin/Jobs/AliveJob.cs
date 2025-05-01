using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Admin.Jobs;

public class AliveJob : BaseJob
{
    private readonly GlobalSettings _globalSettings;
    private readonly HttpClient _httpClient;

    public AliveJob(
        GlobalSettings globalSettings,
        ILogger<AliveJob> logger,
        IHttpClientFactory httpClientFactory)
        : base(logger)
    {
        _globalSettings = globalSettings;
        _httpClient = httpClientFactory.CreateClient();
    }

    protected async override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: Keep alive");
        var response = await _httpClient.GetAsync(_globalSettings.BaseServiceUri.Admin);
        _logger.LogInformation(Constants.BypassFiltersEventId, "Finished job task: Keep alive, " +
            response.StatusCode);
    }
}

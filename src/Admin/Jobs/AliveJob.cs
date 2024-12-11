using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Admin.Jobs;

public class AliveJob : BaseJob
{
    private readonly GlobalSettings _globalSettings;
    private HttpClient _httpClient = new HttpClient();

    public AliveJob(GlobalSettings globalSettings, ILogger<AliveJob> logger)
        : base(logger)
    {
        _globalSettings = globalSettings;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Execute job task: Keep alive");
        var response = await _httpClient.GetAsync(_globalSettings.BaseServiceUri.Admin);
        _logger.LogInformation(
            Constants.BypassFiltersEventId,
            "Finished job task: Keep alive, " + response.StatusCode
        );
    }
}

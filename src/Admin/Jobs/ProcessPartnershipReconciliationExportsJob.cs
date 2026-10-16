using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Bitwarden.Server.Sdk.Features;
using Quartz;

namespace Bit.Admin.Jobs;

[DisallowConcurrentExecution]
public class ProcessPartnershipReconciliationExportsJob : BaseJob
{
    private readonly IProcessPartnershipReconciliationExportsCommand _processPartnershipReconciliationExportsCommand;
    private readonly IFeatureService _featureService;
    private readonly GlobalSettings _globalSettings;

    public ProcessPartnershipReconciliationExportsJob(
        IProcessPartnershipReconciliationExportsCommand processPartnershipReconciliationExportsCommand,
        IFeatureService featureService,
        GlobalSettings globalSettings,
        ILogger<ProcessPartnershipReconciliationExportsJob> logger)
        : base(logger)
    {
        _processPartnershipReconciliationExportsCommand = processPartnershipReconciliationExportsCommand;
        _featureService = featureService;
        _globalSettings = globalSettings;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (_globalSettings.SelfHosted || !_featureService.IsEnabled(FeatureFlagKeys.PartnerSponsorships))
        {
            return;
        }

        var result = await _processPartnershipReconciliationExportsCommand.ProcessAsync();
        if (result.Completed + result.Failed + result.Deleted > 0)
        {
            _logger.LogInformation(Constants.BypassFiltersEventId,
                "Partnership reconciliation exports processed. Completed: {Completed}, Failed: {Failed}, Deleted: {Deleted}",
                result.Completed, result.Failed, result.Deleted);
        }
    }
}

using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Bitwarden.Server.Sdk.Features;
using Quartz;

namespace Bit.Admin.Jobs;

public class ExpirePartnershipResumeWindowsJob : BaseJob
{
    private readonly IExpirePartnershipResumeWindowsCommand _expirePartnershipResumeWindowsCommand;
    private readonly IFeatureService _featureService;
    private readonly GlobalSettings _globalSettings;

    public ExpirePartnershipResumeWindowsJob(
        IExpirePartnershipResumeWindowsCommand expirePartnershipResumeWindowsCommand,
        IFeatureService featureService,
        GlobalSettings globalSettings,
        ILogger<ExpirePartnershipResumeWindowsJob> logger)
        : base(logger)
    {
        _expirePartnershipResumeWindowsCommand = expirePartnershipResumeWindowsCommand;
        _featureService = featureService;
        _globalSettings = globalSettings;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (_globalSettings.SelfHosted || !_featureService.IsEnabled(FeatureFlagKeys.PartnerSponsorships))
        {
            return;
        }

        var result = await _expirePartnershipResumeWindowsCommand.ExpireAsync();
        result.Switch(
            error => _logger.LogError(Constants.BypassFiltersEventId,
                "Partnership resume window expiry failed: {Error}", error.Message),
            released => _logger.LogInformation(Constants.BypassFiltersEventId,
                "Released {ReleasedCount} partnership entitlement(s) with expired resume windows", released));
    }
}

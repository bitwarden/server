using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Services;
using Quartz;

namespace Bit.Services.Pam.AccessConnector.Jobs;

/// <summary>
/// Quartz entry point for <see cref="IPamLeaseExpirySweepService"/> (the lease natural-expiry sweep). Gated on
/// <see cref="FeatureFlagKeys.Pam"/>, not <see cref="FeatureFlagKeys.PamAccessConnector"/>: the rotation trigger it
/// also fires self-gates on that flag further down. Registered from <c>JobsHostedService</c> inside <c>#if !OSS</c>.
/// </summary>
public class PamLeaseExpirySweepJob : BaseJob
{
    private readonly IFeatureService _featureService;
    private readonly IPamLeaseExpirySweepService _sweepService;

    public PamLeaseExpirySweepJob(
        IFeatureService featureService,
        IPamLeaseExpirySweepService sweepService,
        ILogger<PamLeaseExpirySweepJob> logger)
        : base(logger)
    {
        _featureService = featureService;
        _sweepService = sweepService;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.Pam))
        {
            return;
        }

        await _sweepService.SweepAsync();
    }
}

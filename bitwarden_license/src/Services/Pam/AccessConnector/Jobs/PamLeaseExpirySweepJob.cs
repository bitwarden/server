using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Services;
using Quartz;

namespace Bit.Services.Pam.AccessConnector.Jobs;

/// <summary>
/// Quartz entry point for <see cref="IPamLeaseExpirySweepService"/>. Gated on <see cref="FeatureFlagKeys.Pam"/>, not
/// <see cref="FeatureFlagKeys.PamAccessConnector"/>, since the rotation trigger it fires gates itself on the latter.
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

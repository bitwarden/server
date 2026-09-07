using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Services;
using Quartz;

namespace Bit.Services.Pam.AccessConnector.Jobs;

/// <summary>
/// Quartz entry point for <see cref="IPamRotationSweepService"/>. Gated on
/// <see cref="FeatureFlagKeys.PamAccessConnector"/>: the job no-ops on its first line if the flag is off.
/// Registered from <c>JobsHostedService</c> inside <c>#if !OSS</c>.
/// </summary>
public class PamRotationSweepJob : BaseJob
{
    private readonly IFeatureService _featureService;
    private readonly IPamRotationSweepService _sweepService;

    public PamRotationSweepJob(
        IFeatureService featureService,
        IPamRotationSweepService sweepService,
        ILogger<PamRotationSweepJob> logger)
        : base(logger)
    {
        _featureService = featureService;
        _sweepService = sweepService;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.PamAccessConnector))
        {
            return;
        }

        await _sweepService.SweepAsync();
    }
}

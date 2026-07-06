using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Services;
using Quartz;

namespace Bit.Services.Pam.Rotation.Jobs;

/// <summary>
/// Quartz entry point for <see cref="IPamRotationSweepService"/> (spec <c>RotationDue</c>, <c>JobTimesOut</c>,
/// <c>DaemonConnectionDropsReleaseJobs</c>). Gated on <see cref="FeatureFlagKeys.PamRotation"/> -- when the flag is
/// off the job no-ops on its first line, matching every other rotation entry point (see
/// <see cref="Bit.Services.Pam.Rotation.Commands.HandleAccessGrantEndedCommand"/>). Registered from
/// <c>JobsHostedService</c> inside <c>#if !OSS</c>, since the sweep depends on commercial PAM commands.
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
        if (!_featureService.IsEnabled(FeatureFlagKeys.PamRotation))
        {
            return;
        }

        await _sweepService.SweepAsync();
    }
}

using Bit.Core;
using Bit.Core.Jobs;
using Bit.Core.Services;
using Quartz;

namespace Bit.Services.Pam.Rotation.Jobs;

/// <summary>
/// Quartz entry point for <see cref="IPamLeaseExpirySweepService"/> (the lease natural-expiry sweep). Gated on
/// <see cref="FeatureFlagKeys.Pam"/> rather than <see cref="FeatureFlagKeys.PamRotation"/>: flipping an
/// <see cref="Bit.Pam.Enums.AccessLeaseStatus.Active"/> lease to
/// <see cref="Bit.Pam.Enums.AccessLeaseStatus.Expired"/> and emitting the deferred
/// <see cref="Bit.Pam.Enums.AccessAuditEventKind.LeaseExpired"/> event is a leasing fix that belongs to PAM v0, not
/// rotation; the rotation trigger it also fires (via <see cref="IPamLeaseExpirySweepService"/> calling
/// <see cref="Bit.Services.Pam.Rotation.Commands.Interfaces.IHandleAccessGrantEndedCommand"/>) self-gates on
/// <see cref="FeatureFlagKeys.PamRotation"/> further down. Registered from <c>JobsHostedService</c> inside
/// <c>#if !OSS</c>, since the sweep depends on commercial PAM commands.
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

namespace Bit.Services.Pam.Rotation.Jobs;

/// <summary>
/// Runs the lease natural-expiry sweep: flips every <see cref="Bit.Pam.Enums.AccessLeaseStatus.Active"/> lease whose
/// window has closed on its own to <see cref="Bit.Pam.Enums.AccessLeaseStatus.Expired"/>, emits the deferred
/// <see cref="Bit.Pam.Enums.AccessAuditEventKind.LeaseExpired"/> audit event, and fires the rotation access-end
/// trigger for each -- closing the standing PAM gap where a lease's own expiry never produced a
/// <see cref="Bit.Pam.Enums.AccessAuditEventKind.LeaseRevoked"/>-shaped record. Invoked on a Quartz cron by
/// <see cref="PamLeaseExpirySweepJob"/>; kept separate from the job class so the sweep logic itself is testable
/// without a <c>Quartz.IJobExecutionContext</c>.
/// </summary>
public interface IPamLeaseExpirySweepService
{
    /// <summary>
    /// Expires every due lease and, per lease, emits its audit event then calls
    /// <see cref="Bit.Services.Pam.Rotation.Commands.Interfaces.IHandleAccessGrantEndedCommand"/> -- a failure
    /// against one lease is logged and swallowed rather than propagated, so it never prevents the rest of the batch
    /// from being processed.
    /// </summary>
    Task SweepAsync();
}

namespace Bit.Services.Pam.AccessConnector.Jobs;

/// <summary>
/// Runs the lease natural-expiry sweep: finds every lease whose window has closed on its own, emits the deferred
/// <see cref="Bit.Pam.Enums.AccessAuditEventKind.LeaseExpired"/> audit event, and fires the rotation access-end
/// trigger for each. Invoked on a Quartz cron by <see cref="PamLeaseExpirySweepJob"/>; kept separate so the sweep
/// logic is testable without a <c>Quartz.IJobExecutionContext</c>.
/// </summary>
public interface IPamLeaseExpirySweepService
{
    /// <summary>
    /// Expires every due lease and, per lease, emits its audit event then calls
    /// <see cref="Bit.Services.Pam.AccessConnector.Commands.Interfaces.IHandleAccessGrantEndedCommand"/> -- a failure
    /// against one lease is logged and swallowed rather than propagated, so it never prevents the rest of the batch
    /// from being processed.
    /// </summary>
    Task SweepAsync();
}

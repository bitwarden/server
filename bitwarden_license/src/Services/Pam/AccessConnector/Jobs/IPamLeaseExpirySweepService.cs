namespace Bit.Services.Pam.AccessConnector.Jobs;

/// <summary>
/// The lease natural-expiry sweep: for each lease whose window has closed, emits the deferred
/// <see cref="Bit.Pam.Enums.AccessAuditEventKind.LeaseExpired"/> event and fires the rotation access-end trigger.
/// </summary>
public interface IPamLeaseExpirySweepService
{
    /// <summary>A failure on one lease is logged and swallowed, so it does not stop the rest of the batch.</summary>
    Task SweepAsync();
}

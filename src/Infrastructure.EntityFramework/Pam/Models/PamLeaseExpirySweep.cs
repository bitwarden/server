#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The natural-expiry sweep's journal: one row per lease already returned, so none is returned twice. Pure
/// bookkeeping, with no <c>Bit.Pam</c> entity.
/// </summary>
public class PamLeaseExpirySweep
{
    public Guid AccessLeaseId { get; set; }
    public DateTime SweptDate { get; set; }
}

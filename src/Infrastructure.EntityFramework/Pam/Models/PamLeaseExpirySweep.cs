#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The natural-expiry sweep's journal, mirroring [dbo].[PamLeaseExpirySweep]: one row per lease
/// <c>IAccessLeaseRepository.ExpireDueAsync</c> has already returned. Expiry is derived at read time rather than
/// stored, so this journal — not a status flip — is what keeps the sweep from returning a lease twice. Pure
/// persistence bookkeeping: nothing reads it back as a domain object, so it has no <c>Bit.Pam</c> counterpart.
/// </summary>
public class PamLeaseExpirySweep
{
    public Guid AccessLeaseId { get; set; }
    public DateTime SweptDate { get; set; }
}

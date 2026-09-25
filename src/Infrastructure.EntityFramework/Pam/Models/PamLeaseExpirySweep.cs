#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The natural-expiry sweep's journal, mirroring [dbo].[PamLeaseExpirySweep]: one row per lease
/// <c>IAccessLeaseRepository.ExpireDueAsync</c> has already returned.
/// This journal, not a status flip, keeps the sweep from returning a lease twice; pure bookkeeping with no
/// <c>Bit.Pam</c> counterpart.
/// </summary>
public class PamLeaseExpirySweep
{
    public Guid AccessLeaseId { get; set; }
    public DateTime SweptDate { get; set; }
}

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// An access end still owed its rotation trigger, one row per ended lease. Pure bookkeeping, with no <c>Bit.Pam</c>
/// entity.
/// </summary>
public class PamPendingAccessEnd
{
    public Guid AccessLeaseId { get; set; }
    public Guid RotationConfigId { get; set; }
    public DateTime CreationDate { get; set; }
}

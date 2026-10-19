#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The timeout sweep's journal, in place of a status flip: one row per job already returned, so none is returned
/// twice, and the row ends the job's hold on its config. Pure bookkeeping, with no <c>Bit.Pam</c> entity.
/// </summary>
public class PamRotationJobTimeoutSweep
{
    public Guid RotationJobId { get; set; }
    public DateTime SweptDate { get; set; }
}

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The timeout sweep's journal, mirroring [dbo].[PamRotationJobTimeoutSweep]: one row per job
/// <c>IPamRotationJobRepository.TimeoutDueAsync</c> has already returned.
/// This journal, not a status flip, keeps the sweep from returning a job twice and ends the job's hold on its config;
/// pure bookkeeping with no <c>Bit.Pam</c> counterpart.
/// </summary>
public class PamRotationJobTimeoutSweep
{
    public Guid RotationJobId { get; set; }
    public DateTime SweptDate { get; set; }
}

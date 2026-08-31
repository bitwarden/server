using Bit.Pam.Entities;

namespace Bit.Pam.Models;

/// <summary>
/// A claimable <see cref="PamRotationJob"/> together with the target system it rotates against — the read model for
/// a daemon's poll (<c>GET rotation/daemon/jobs</c>). The job row carries only <c>RotationConfigId</c>, and the poll
/// query already joins <see cref="PamRotationConfig"/> to check eligibility, so the target id is projected there
/// rather than re-read per job.
/// </summary>
public class PamClaimableJob : PamRotationJob
{
    public Guid TargetSystemId { get; set; }
}

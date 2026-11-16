using Bit.Pam.Entities;

namespace Bit.Pam.Models;

/// <summary>
/// A claimable <see cref="PamRotationJob"/> with its target system, for an access connector's poll. The poll query
/// already joins <see cref="PamRotationConfig"/>, so the target id is projected there rather than read per job.
/// </summary>
public class PamClaimableJob : PamRotationJob
{
    public Guid TargetSystemId { get; set; }
}

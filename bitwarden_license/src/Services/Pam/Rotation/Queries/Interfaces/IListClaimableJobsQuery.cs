using Bit.Pam.Entities;

namespace Bit.Services.Pam.Rotation.Queries.Interfaces;

public interface IListClaimableJobsQuery
{
    /// <summary>
    /// A daemon's currently claimable jobs — its poll (spec <c>ClaimRotation</c>'s candidate set). Doubles as the
    /// daemon's heartbeat when idle.
    /// </summary>
    Task<ICollection<PamRotationJob>> ListAsync(Guid daemonId);
}

using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IPamDaemonRepository : IRepository<PamDaemon, Guid>
{
    Task<ICollection<PamDaemon>> GetManyByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Returns the daemon's <see cref="PamDaemonDetails"/> — including its organization's licensing state — by the
    /// id of its <see cref="PamDaemon.ApiKeyId"/> credential, or null if no daemon references that key.
    /// <c>PamDaemonClientProvider</c> loads this on every token request.
    /// </summary>
    Task<PamDaemonDetails?> GetDetailsByApiKeyIdAsync(Guid apiKeyId);

    /// <summary>
    /// Bumps <see cref="PamDaemon.LastHeartbeatAt"/> to <paramref name="now"/>, but only when the stored value is
    /// null or older than <paramref name="now"/> minus <paramref name="minInterval"/> — a conditional write so a
    /// polling daemon does not hammer the row on every request.
    /// </summary>
    Task UpdateHeartbeatAsync(Guid daemonId, DateTime now, TimeSpan minInterval);

    /// <summary>
    /// Records a daemon's assignment to a target system (invariant <c>OneAssignmentPerDaemonTarget</c>). The
    /// assignment must already have its id assigned.
    /// </summary>
    Task CreateAssignmentAsync(PamDaemonTargetAssignment assignment);

    Task DeleteAssignmentAsync(Guid daemonId, Guid targetSystemId);

    Task<ICollection<PamDaemonTargetAssignment>> GetAssignmentsByOrganizationIdAsync(Guid organizationId);

    Task<bool> AssignmentExistsAsync(Guid daemonId, Guid targetSystemId);
}

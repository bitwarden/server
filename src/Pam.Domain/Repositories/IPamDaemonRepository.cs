using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

/// <remarks>
/// <c>DeleteAsync</c> is a cascade: in one transaction it clears the daemon's target assignments, releases its
/// held jobs, deletes the daemon, and deletes the <c>dbo.ApiKey</c> row that authenticates it.
/// </remarks>
public interface IPamDaemonRepository : IRepository<PamDaemon, Guid>
{
    Task<ICollection<PamDaemon>> GetManyByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Returns the daemon's <see cref="PamDaemonDetails"/> — including its organization's licensing state — by
    /// the id of its <see cref="PamDaemon.ApiKeyId"/> credential, or null. Loaded by
    /// <c>PamDaemonClientProvider</c> on every token request.
    /// </summary>
    Task<PamDaemonDetails?> GetDetailsByApiKeyIdAsync(Guid apiKeyId);

    /// <summary>
    /// Bumps <see cref="PamDaemon.LastHeartbeatAt"/> to <paramref name="now"/>. A conditional write, skipped
    /// unless the stored value is null or older than <paramref name="now"/> minus <paramref name="minInterval"/>,
    /// so a polling daemon does not hammer the row on every request.
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

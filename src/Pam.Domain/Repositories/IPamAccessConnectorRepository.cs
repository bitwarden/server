using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

/// <remarks>
/// <c>DeleteAsync</c> cascades in one transaction: it releases the access connector's claimed jobs and deletes its
/// target assignments, the access connector, and its <c>dbo.ApiKey</c> credential.
/// </remarks>
public interface IPamAccessConnectorRepository : IRepository<PamAccessConnector, Guid>
{
    Task<ICollection<PamAccessConnector>> GetManyByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Looks up the access connector by its credential's id, with its organization's licensing state.
    /// </summary>
    Task<PamAccessConnectorDetails?> GetDetailsByApiKeyIdAsync(Guid apiKeyId);

    /// <summary>
    /// Bumps <see cref="PamAccessConnector.LastHeartbeatAt"/> only when it is null or older than
    /// <paramref name="minInterval"/>, so a polling access connector does not write on every request.
    /// </summary>
    Task UpdateHeartbeatAsync(Guid accessConnectorId, DateTime now, TimeSpan minInterval);

    /// <summary>
    /// The assignment must already have its id. A unique index backstops <c>OneAssignmentPerConnectorTarget</c>.
    /// </summary>
    Task CreateAssignmentAsync(PamAccessConnectorTargetAssignment assignment);

    Task DeleteAssignmentAsync(Guid accessConnectorId, Guid targetSystemId);

    Task<ICollection<PamAccessConnectorTargetAssignment>> GetAssignmentsByOrganizationIdAsync(Guid organizationId);

    Task<bool> AssignmentExistsAsync(Guid accessConnectorId, Guid targetSystemId);
}

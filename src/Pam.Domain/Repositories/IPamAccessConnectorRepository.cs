using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

/// <remarks>
/// <c>DeleteAsync</c> is a cascade: in one transaction it clears the access connector's target assignments, releases
/// its held jobs, deletes the access connector, and deletes the <c>dbo.ApiKey</c> row that authenticates it.
/// </remarks>
public interface IPamAccessConnectorRepository : IRepository<PamAccessConnector, Guid>
{
    Task<ICollection<PamAccessConnector>> GetManyByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Returns the access connector's <see cref="PamAccessConnectorDetails"/> — including its organization's licensing
    /// state — by the id of its <see cref="PamAccessConnector.ApiKeyId"/> credential, or null. Loaded by
    /// <c>PamAccessConnectorClientProvider</c> on every token request.
    /// </summary>
    Task<PamAccessConnectorDetails?> GetDetailsByApiKeyIdAsync(Guid apiKeyId);

    /// <summary>
    /// Bumps <see cref="PamAccessConnector.LastHeartbeatAt"/> to <paramref name="now"/>. A conditional write, skipped
    /// unless the stored value is null or older than <paramref name="now"/> minus <paramref name="minInterval"/>,
    /// so a polling access connector does not hammer the row on every request.
    /// </summary>
    Task UpdateHeartbeatAsync(Guid accessConnectorId, DateTime now, TimeSpan minInterval);

    /// <summary>
    /// Records an access connector's assignment to a target system (invariant <c>OneAssignmentPerConnectorTarget</c>).
    /// The assignment must already have its id assigned.
    /// </summary>
    Task CreateAssignmentAsync(PamAccessConnectorTargetAssignment assignment);

    Task DeleteAssignmentAsync(Guid accessConnectorId, Guid targetSystemId);

    Task<ICollection<PamAccessConnectorTargetAssignment>> GetAssignmentsByOrganizationIdAsync(Guid organizationId);

    Task<bool> AssignmentExistsAsync(Guid accessConnectorId, Guid targetSystemId);
}

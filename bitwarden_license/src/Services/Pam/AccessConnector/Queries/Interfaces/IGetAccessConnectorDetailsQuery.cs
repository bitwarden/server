using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Queries.Interfaces;

public interface IGetAccessConnectorDetailsQuery
{
    /// <summary>
    /// A single access connector's detail view: derived connection state, target assignments, and recent rotation
    /// activity. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> if the access connector doesn't exist or
    /// belongs to a different organization.
    /// </summary>
    Task<PamAccessConnectorHistory> GetAsync(Guid organizationId, Guid accessConnectorId);
}

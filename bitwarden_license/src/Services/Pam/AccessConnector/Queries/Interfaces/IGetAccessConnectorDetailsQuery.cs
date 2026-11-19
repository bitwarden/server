using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Queries.Interfaces;

public interface IGetAccessConnectorDetailsQuery
{
    /// <summary>
    /// An access connector's detail view. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> if it does not
    /// exist or belongs to another organization.
    /// </summary>
    Task<PamAccessConnectorHistory> GetAsync(Guid organizationId, Guid accessConnectorId);
}

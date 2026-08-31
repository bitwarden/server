using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Queries.Interfaces;

public interface IGetAccessConnectorDetailsQuery
{
    /// <summary>
    /// A single daemon's detail view: its derived connection state and target assignments, plus its recent rotation
    /// activity. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> when the daemon does not exist or belongs
    /// to a different organization.
    /// </summary>
    Task<PamAccessConnectorHistory> GetAsync(Guid organizationId, Guid daemonId);
}

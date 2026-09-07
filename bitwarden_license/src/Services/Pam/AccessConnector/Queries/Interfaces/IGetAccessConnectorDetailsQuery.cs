using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Queries.Interfaces;

public interface IGetAccessConnectorDetailsQuery
{
    /// <summary>
    /// A single daemon's detail view: derived connection state, target assignments, and recent rotation activity.
    /// Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> if the daemon doesn't exist or belongs to a
    /// different organization.
    /// </summary>
    Task<PamAccessConnectorHistory> GetAsync(Guid organizationId, Guid daemonId);
}

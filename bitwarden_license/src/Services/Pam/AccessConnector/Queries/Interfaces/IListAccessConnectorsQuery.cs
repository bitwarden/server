using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Queries.Interfaces;

public interface IListAccessConnectorsQuery
{
    /// <summary>The daemons list view for an organization, with derived connection state and target assignments.</summary>
    Task<ICollection<PamAccessConnectorListItem>> ListAsync(Guid organizationId);
}

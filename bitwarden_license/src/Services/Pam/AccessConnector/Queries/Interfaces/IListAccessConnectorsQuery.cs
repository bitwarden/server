using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Queries.Interfaces;

public interface IListAccessConnectorsQuery
{
    /// <summary>An organization's access connectors, with derived connection state and target assignments.</summary>
    Task<ICollection<PamAccessConnectorListItem>> ListAsync(Guid organizationId);
}

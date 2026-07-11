using Bit.Services.Pam.Rotation.Models;

namespace Bit.Services.Pam.Rotation.Queries.Interfaces;

public interface IListDaemonsQuery
{
    /// <summary>The daemons list view for an organization, with derived connection state and target assignments.</summary>
    Task<ICollection<PamDaemonListItem>> ListAsync(Guid organizationId);
}

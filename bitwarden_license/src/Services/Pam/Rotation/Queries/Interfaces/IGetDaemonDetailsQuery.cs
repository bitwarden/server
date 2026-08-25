using Bit.Services.Pam.Rotation.Models;

namespace Bit.Services.Pam.Rotation.Queries.Interfaces;

public interface IGetDaemonDetailsQuery
{
    /// <summary>
    /// A single daemon's detail view: its derived connection state and target assignments, plus its recent rotation
    /// activity. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> when the daemon does not exist or belongs
    /// to a different organization.
    /// </summary>
    Task<PamDaemonHistory> GetAsync(Guid organizationId, Guid daemonId);
}

using Bit.Services.Pam.AccessConnector.Models;

namespace Bit.Services.Pam.AccessConnector.Queries.Interfaces;

public interface IGetRotationConfigDetailsQuery
{
    /// <summary>
    /// A rotation config with its job and attempt history. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/>
    /// if it does not exist or belongs to another organization.
    /// </summary>
    Task<PamRotationConfigHistory> GetAsync(Guid organizationId, Guid configId);
}

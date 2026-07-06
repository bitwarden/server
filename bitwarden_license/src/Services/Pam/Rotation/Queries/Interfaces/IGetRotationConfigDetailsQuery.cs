using Bit.Services.Pam.Rotation.Models;

namespace Bit.Services.Pam.Rotation.Queries.Interfaces;

public interface IGetRotationConfigDetailsQuery
{
    /// <summary>
    /// A single rotation config's detail view, including its job/attempt history. Throws
    /// <see cref="Bit.Core.Exceptions.NotFoundException"/> when the config does not exist or belongs to a different
    /// organization.
    /// </summary>
    Task<PamRotationConfigHistory> GetAsync(Guid organizationId, Guid configId);
}

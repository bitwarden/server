using Bit.Pam.Entities;

namespace Bit.Services.Pam.Rotation.Queries.Interfaces;

public interface IListTargetSystemsQuery
{
    /// <summary>The target systems registered for an organization.</summary>
    Task<ICollection<PamTargetSystem>> ListAsync(Guid organizationId);
}

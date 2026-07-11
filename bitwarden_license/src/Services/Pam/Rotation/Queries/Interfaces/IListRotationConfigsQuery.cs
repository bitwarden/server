using Bit.Pam.Models;

namespace Bit.Services.Pam.Rotation.Queries.Interfaces;

public interface IListRotationConfigsQuery
{
    /// <summary>The rotation-configs schedule list view for an organization.</summary>
    Task<ICollection<PamRotationConfigDetails>> ListAsync(Guid organizationId);
}

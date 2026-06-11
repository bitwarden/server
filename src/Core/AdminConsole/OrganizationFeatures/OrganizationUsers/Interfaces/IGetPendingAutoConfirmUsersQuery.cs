using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IGetPendingAutoConfirmUsersQuery
{
    /// <summary>
    /// Returns organization users that are pending auto-confirmation for the given organization.
    /// Returns an empty collection if the organization does not have automatic user confirmation enabled
    /// or if the AutomaticUserConfirmation policy is not enabled.
    /// </summary>
    Task<ICollection<OrganizationUser>> GetPendingAutoConfirmUsersAsync(Guid organizationId);
}

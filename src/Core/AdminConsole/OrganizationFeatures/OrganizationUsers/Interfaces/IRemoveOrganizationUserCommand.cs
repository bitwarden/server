using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IRemoveOrganizationUserCommand
{
    Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);

    Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser);
    Task RemoveUserAsync(Guid organizationId, Guid userId);
    Task<List<Tuple<OrganizationUser, string>>> RemoveUsersAsync(Guid organizationId,
        IEnumerable<Guid> organizationUserIds, Guid? deletingUserId);
    /// <summary>
    /// Removes a user from an organization when they have left voluntarily. This should only be called by the same user who is being removed.
    /// </summary>
    /// <param name="organizationId">Organization to leave.</param>
    /// <param name="userId">User to leave.</param>
    Task UserLeaveAsync(Guid organizationId, Guid userId);
}

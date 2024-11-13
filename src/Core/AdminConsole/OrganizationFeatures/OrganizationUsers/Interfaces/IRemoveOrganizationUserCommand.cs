using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IRemoveOrganizationUserCommand
{
    Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);

    Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser);
    Task RemoveUserAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Removes multiple users from an organization.
    /// </summary>
    /// <returns>
    /// A list of tuples containing the organization user id and the error message for each user that could not be removed, otherwise empty.
    /// </returns>
    Task<IEnumerable<(Guid OrganizationUserId, string ErrorMessage)>> RemoveUsersAsync(
        Guid organizationId, IEnumerable<Guid> organizationUserIds, Guid? deletingUserId);
}

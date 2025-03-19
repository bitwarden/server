using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreOrganizationUser;

public interface IRestoreOrganizationUserCommand
{
    Task RestoreUserAsync(OrganizationUser organizationUser, Guid? restoringUserId);
    Task RestoreUserAsync(OrganizationUser organizationUser, EventSystemUser systemUser);
    Task<List<Tuple<OrganizationUser, string>>> RestoreUsersAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds, Guid? restoringUserId, IUserService userService);
}

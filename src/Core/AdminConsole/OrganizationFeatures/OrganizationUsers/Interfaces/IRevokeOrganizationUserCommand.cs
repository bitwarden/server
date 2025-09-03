using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IRevokeOrganizationUserCommand
{
    Task RevokeUserAsync(OrganizationUser organizationUser, Guid? revokingUserId);
    Task RevokeUserAsync(OrganizationUser organizationUser, EventSystemUser systemUser);
    Task<List<Tuple<OrganizationUser, string>>> RevokeUsersAsync(Guid organizationId,
        IEnumerable<Guid> organizationUserIds, Guid? revokingUserId);
}

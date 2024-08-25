using Bit.Core.AdminConsole.Enums;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteOrganizationUserCommand
{
    Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId, OrganizationUserRemovalType removalType = OrganizationUserRemovalType.AdminRemoved);
    Task DeleteUsersAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds, Guid? deletingUserId, OrganizationUserRemovalType removalType = OrganizationUserRemovalType.AdminRemoved);

    Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser);
}



using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IRemoveOrganizationUserCommand
{
    Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);

    Task RemoveUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser);
    Task RemoveUserAsync(Guid organizationId, Guid userId);
}

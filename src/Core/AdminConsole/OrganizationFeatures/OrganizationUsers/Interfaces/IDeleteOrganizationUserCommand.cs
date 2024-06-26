using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteOrganizationUserCommand
{
    Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);

    Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, EventSystemUser eventSystemUser);
}

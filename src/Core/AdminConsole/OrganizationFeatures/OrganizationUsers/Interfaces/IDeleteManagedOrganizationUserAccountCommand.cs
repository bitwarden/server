using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteManagedOrganizationUserAccountCommand
{
    Task DeleteUserAsync(Guid organizationId, Guid organizationUserId);
    Task<IEnumerable<(OrganizationUser, string)>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds);
}

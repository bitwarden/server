namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteManagedOrganizationUserAccountCommand
{
    Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);
    Task<IEnumerable<(Guid, string)>> DeleteManyUsersAsync(Guid organizationId, IEnumerable<Guid> orgUserIds, Guid? deletingUserId);
}

namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteOrganizationUserCommand
{
    Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);
}

namespace Bit.Core.Commands.Interfaces;

public interface IDeleteOrganizationUserCommand
{
    Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);
}

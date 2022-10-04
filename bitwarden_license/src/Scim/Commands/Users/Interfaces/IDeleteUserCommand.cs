namespace Bit.Scim.Commands.Users.Interfaces;

public interface IDeleteUserCommand
{
    Task DeleteUserAsync(Guid organizationId, Guid id);
}

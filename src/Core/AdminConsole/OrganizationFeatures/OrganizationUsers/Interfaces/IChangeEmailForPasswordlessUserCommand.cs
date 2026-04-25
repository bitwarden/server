using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IChangeEmailForPasswordlessUserCommand
{
    Task ChangeEmailAsync(Guid organizationId, OrganizationUser organizationUser, string newEmail);
}

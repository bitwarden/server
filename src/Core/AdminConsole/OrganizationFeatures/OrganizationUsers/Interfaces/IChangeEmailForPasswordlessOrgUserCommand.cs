using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IChangeEmailForPasswordlessOrgUserCommand
{
    Task ChangeOrganizationUserEmailAsync(Guid organizationId, OrganizationUser organizationUser, string newEmail);
}

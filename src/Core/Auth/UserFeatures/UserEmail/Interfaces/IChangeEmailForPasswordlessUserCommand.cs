using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.UserEmail.Interfaces;

public interface IChangeEmailForPasswordlessUserCommand
{
    Task ChangeOrganizationUserEmailAsync(Guid organizationId, OrganizationUser organizationUser, string newEmail);
}

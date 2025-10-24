using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public interface IAutomaticallyConfirmOrganizationUsersValidator
{
    Task<ValidationResult<AutomaticallyConfirmOrganizationUserRequestData>> ValidateAsync(
        AutomaticallyConfirmOrganizationUserRequestData request);
}

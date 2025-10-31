using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public interface IAutomaticallyConfirmOrganizationUsersValidator
{
    Task<ValidationResult<AutomaticallyConfirmOrganizationUserRequestData>> ValidateAsync(
        AutomaticallyConfirmOrganizationUserRequestData request);
}

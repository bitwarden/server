using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

public interface IUpdateOrganizationUserValidator
{
    /// <summary>
    /// Validates an organization user update. On success, the returned request is ready to persist.
    /// </summary>
    Task<ValidationResult<UpdateOrganizationUserRequest>> ValidateAsync(
        UpdateOrganizationUserRequest request);
}

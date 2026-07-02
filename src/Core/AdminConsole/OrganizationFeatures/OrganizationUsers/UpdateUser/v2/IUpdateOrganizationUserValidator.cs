using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

public interface IUpdateOrganizationUserValidator
{
    /// <summary>
    /// Validates an organization user update. On success, the returned request carries the
    /// collection access list with default user collections filtered out, ready to persist.
    /// </summary>
    Task<ValidationResult<UpdateOrganizationUserValidationRequest>> ValidateAsync(
        UpdateOrganizationUserValidationRequest request);
}

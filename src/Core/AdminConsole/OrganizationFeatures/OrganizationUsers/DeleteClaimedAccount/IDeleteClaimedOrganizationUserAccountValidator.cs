using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

public interface IDeleteClaimedOrganizationUserAccountValidator
{
    Task<IEnumerable<ValidationResult<DeleteUserValidationRequest>>> ValidateAsync(IEnumerable<DeleteUserValidationRequest> requests);
}

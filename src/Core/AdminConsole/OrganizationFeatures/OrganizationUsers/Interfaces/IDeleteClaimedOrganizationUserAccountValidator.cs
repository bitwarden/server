using Bit.Core.AdminConsole.Utilities.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteClaimedOrganizationUserAccountValidator
{
    Task<IEnumerable<ValidationResult<DeleteUserValidationRequest>>> ValidateAsync(List<DeleteUserValidationRequest> requests);
}

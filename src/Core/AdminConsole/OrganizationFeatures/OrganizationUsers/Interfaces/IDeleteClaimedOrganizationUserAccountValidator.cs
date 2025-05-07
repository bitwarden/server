using Bit.Core.AdminConsole.Utilities.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteClaimedOrganizationUserAccountValidator
{
    Task<PartialValidationResult<DeleteUserValidationRequest>> ValidateAsync(List<DeleteUserValidationRequest> requests);
}

using Bit.Core.AdminConsole.Shared.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteManagedOrganizationUserAccountValidator
{
    Task<PartialValidationResult<DeleteUserValidationRequest>> ValidateAsync(List<DeleteUserValidationRequest> requests);
}

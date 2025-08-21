using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IDeleteClaimedOrganizationUserAccountValidator
{
    Task<IEnumerable<ValidationResult<DeleteUserValidationRequest>>> ValidateAsync(IEnumerable<DeleteUserValidationRequest> requests);
}

using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

public interface IRevokeOrganizationUserValidator
{
    Task<ICollection<ValidationResult<OrganizationUser>>> ValidateAsync(RevokeOrganizationUsersValidationRequest request);
}

using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

public class RevokeOrganizationUsersValidator : IRevokeOrganizationUserValidator
{
    public ICollection<ValidationResult<OrganizationUser>> Validate(RevokeOrganizationUsersValidationRequest request) =>
        request.OrganizationUsersToRevoke.Select(x =>
        {
            return x switch
            {
                _ when x.UserId is not null && x.UserId == request.PerformedBy.UserId =>
                    Invalid(x, new CannotRevokeYourself()),
                { Status: OrganizationUserStatusType.Revoked } =>
                    Invalid(x, new UserAlreadyRevoked()),
                { Type: OrganizationUserType.Owner } when !request.PerformedBy.IsOrganizationOwnerOrProvider =>
                    Invalid(x, new OnlyOwnersCanRevokeOwners()),

                _ => Valid(x)
            };
        }).ToList();
}

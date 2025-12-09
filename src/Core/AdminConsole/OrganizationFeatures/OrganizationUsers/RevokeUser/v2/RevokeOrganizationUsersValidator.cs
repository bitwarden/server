using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

public class RevokeOrganizationUsersValidator(IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery)
    : IRevokeOrganizationUserValidator
{
    public async Task<ICollection<ValidationResult<OrganizationUser>>> ValidateAsync(
        RevokeOrganizationUsersValidationRequest request)
    {
        var hasRemainingOwner = await hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(request.OrganizationId,
            request.OrganizationUsersToRevoke.Select(x => x.Id) // users excluded because they are going to be revoked
            );

        return request.OrganizationUsersToRevoke.Select(x =>
        {
            return x switch
            {
                _ when request.PerformedBy is not SystemUser
                       && x.UserId is not null
                       && x.UserId == request.PerformedBy.UserId =>
                    Invalid(x, new CannotRevokeYourself()),
                { Status: OrganizationUserStatusType.Revoked } =>
                    Invalid(x, new UserAlreadyRevoked()),
                { Type: OrganizationUserType.Owner } when !hasRemainingOwner =>
                    Invalid(x, new MustHaveConfirmedOwner()),
                { Type: OrganizationUserType.Owner } when !request.PerformedBy.IsOrganizationOwnerOrProvider =>
                    Invalid(x, new OnlyOwnersCanRevokeOwners()),

                _ => Valid(x)
            };
        }).ToList();
    }
}

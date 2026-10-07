using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

public class RevokeOrganizationUsersValidator(
    IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
    ICurrentContext currentContext)
    : IRevokeOrganizationUserValidator
{
    public async Task<ICollection<ValidationResult<OrganizationUser>>> ValidateAsync(
        RevokeOrganizationUsersValidationRequest request)
    {
        var hasRemainingOwner = await hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(request.OrganizationId,
            request.OrganizationUsersToRevoke.Select(x => x.Id) // users excluded because they are going to be revoked
            );

        var isCustomUser = request.PerformedBy is not SystemUser
                           && !await currentContext.OrganizationAdmin(request.OrganizationId);

        return request.OrganizationUsersToRevoke.Select(organizationUser =>
        {
            return organizationUser switch
            {
                _ when request.PerformedBy is not SystemUser
                       && organizationUser.UserId is not null
                       && organizationUser.UserId == request.PerformedBy.UserId =>
                    Invalid(organizationUser, new CannotRevokeYourself()),
                { Status: OrganizationUserStatusType.Revoked } =>
                    Invalid(organizationUser, new UserAlreadyRevoked()),
                { Type: OrganizationUserType.Owner } when !hasRemainingOwner =>
                    Invalid(organizationUser, new MustHaveConfirmedOwner()),
                { Type: OrganizationUserType.Owner } when request.PerformedBy is not SystemUser
                                                        && !request.PerformedBy.IsOrganizationOwnerOrProvider =>
                    Invalid(organizationUser, new OnlyOwnersCanRevokeOwners()),
                { Type: OrganizationUserType.Admin } when isCustomUser =>
                    Invalid(organizationUser, new CustomUsersCannotRevokeAdmins()),

                _ => Valid(organizationUser)
            };
        }).ToList();
    }
}

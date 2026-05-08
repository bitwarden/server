using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validators;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

public class RevokeOrganizationUsersValidator(
    IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
    ICustomUserActingOnAdminValidator customUserActingOnAdminValidator)
    : IRevokeOrganizationUserValidator
{
    public async Task<ICollection<ValidationResult<OrganizationUser>>> ValidateAsync(
        RevokeOrganizationUsersValidationRequest request)
    {
        var hasRemainingOwner = await hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(request.OrganizationId,
            request.OrganizationUsersToRevoke.Select(x => x.Id) // users excluded because they are going to be revoked
            );

        var customUserCannotRevokeAdmin = await CustomUserCannotRevokeAdminAsync(request.OrganizationUsersToRevoke);

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
                { Type: OrganizationUserType.Owner } when request.PerformedBy is not SystemUser
                                                        && !request.PerformedBy.IsOrganizationOwnerOrProvider =>
                    Invalid(x, new OnlyOwnersCanRevokeOwners()),
                { Type: OrganizationUserType.Admin } when customUserCannotRevokeAdmin =>
                    Invalid(x, new CustomUserCannotRevokeAdmin()),

                _ => Valid(x)
            };
        }).ToList();
    }

    // Probes with any admin in the batch. The rule's answer is uniform across every admin
    // in the same organization, so one cached lookup covers the whole batch.
    private async Task<bool> CustomUserCannotRevokeAdminAsync(IEnumerable<OrganizationUser> users)
    {
        var anyAdmin = users.FirstOrDefault(x => x.Type == OrganizationUserType.Admin);
        return anyAdmin is not null
            && await customUserActingOnAdminValidator.IsBlockedAsync(anyAdmin);
    }
}

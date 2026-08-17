using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

public class RevokeOrganizationUsersValidator(
    IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
    IOrganizationUserValidationService organizationUserValidationService)
    : IRevokeOrganizationUserValidator
{
    public async Task<ICollection<ValidationResult<OrganizationUser>>> ValidateAsync(
        RevokeOrganizationUsersValidationRequest request)
    {
        var hasRemainingOwner = await hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(request.OrganizationId,
            request.OrganizationUsersToRevoke.Select(x => x.Id) // users excluded because they are going to be revoked
            );

        var manageErrorsByTarget = await GetManageErrorsAsync(request);

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
                _ when manageErrorsByTarget[organizationUser.Id].TryGetError(out var manageError) =>
                    Invalid(organizationUser, manageError!),

                _ => Valid(organizationUser)
            };
        }).ToList();
    }

    /// <summary>
    /// Delegates the "can the acting user manage this target's role" decision to
    /// <see cref="IOrganizationUserValidationService"/>'s bulk <c>CanManageAsync</c> overload.
    /// System users (SCIM, Public API) skip the check entirely.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, ManageAuthorizationResult>> GetManageErrorsAsync(RevokeOrganizationUsersValidationRequest request)
    {
        var targetsById = request.OrganizationUsersToRevoke.ToDictionary(u => u.Id, u => (IOrganizationUserRole)u);

        if (request.PerformedBy is not StandardUser standardUser)
        {
            return targetsById.ToDictionary(kvp => kvp.Key, _ => ManageAuthorizationResult.Authorized);
        }

        var actingUser = standardUser.OrganizationUserType.HasValue
            ? new OrganizationUserRole(standardUser.OrganizationUserType.Value, request.OrganizationId, standardUser.Permissions)
            : null;

        return await organizationUserValidationService.CanManageAsync(
            standardUser.UserId!.Value, actingUser, request.OrganizationId, targetsById);
    }
}

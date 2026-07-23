using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

/// <inheritdoc />
public class OrganizationUserValidationService(
    IProviderUserRepository providerUserRepository,
    IOrganizationUserRepository organizationUserRepository) : IOrganizationUserValidationService
{
    public async Task<Error?> CanManageAsync(Guid actingUserId, IOrganizationUserRole? actingUser, IOrganizationUserRole targetUser)
    {
        if (IsAuthorizedByRole(actingUser, targetUser.Type) || await IsProviderAsync(actingUserId, targetUser.OrganizationId))
        {
            return null;
        }

        return CannotManageError(targetUser.Type);
    }

    public async Task<Error?> CanManageRoleChangeAsync(Guid actingUserId, IOrganizationUserRole actingUser,
        IOrganizationUserRole targetUser, IOrganizationUserRole newTargetUser)
    {
        // Must be able to manage both the current and requested role.
        var authorizedByRole = IsAuthorizedByRole(actingUser, targetUser.Type)
                               && IsAuthorizedByRole(actingUser, newTargetUser.Type);

        if (!authorizedByRole && !await IsProviderAsync(actingUserId, targetUser.OrganizationId))
        {
            return CannotManageError(targetUser.Type, newTargetUser.Type);
        }

        return ValidateCustomPermissionsGrant(actingUser, newTargetUser);
    }

    public async Task<Error?> ValidateFreeOrgAdminLimitAsync(Guid? userId, PlanType planType,
        OrganizationUserType currentUserType, OrganizationUserType newUserType)
    {
        if (planType != PlanType.Free
            || !userId.HasValue
            || newUserType is not (OrganizationUserType.Admin or OrganizationUserType.Owner))
        {
            return null;
        }

        var freeOrgAdminCount = await organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(userId.Value);

        // The count already includes this organization when the member is currently an Admin or Owner, so allow one.
        var alreadyCounted = currentUserType is OrganizationUserType.Admin or OrganizationUserType.Owner ? 1 : 0;

        return freeOrgAdminCount > alreadyCounted ? new CannotBeAdminOfMultipleFreeOrganizations() : null;
    }

    private static CustomUsersCanOnlyGrantOwnPermissions? ValidateCustomPermissionsGrant(
        IOrganizationUserRole actingUser, IOrganizationUserRole newTargetUser)
    {
        var newTargetPermissions = newTargetUser.GetPermissions();

        // Owners and Admins can grant any custom permission; the check only applies to a Custom grantor.
        if (newTargetUser.Type != OrganizationUserType.Custom
            || newTargetPermissions is null
            || actingUser.Type is OrganizationUserType.Owner or OrganizationUserType.Admin)
        {
            return null;
        }

        var actorClaims = (actingUser.GetPermissions() ?? new Permissions())
            .ClaimsMap.ToDictionary(c => c.ClaimName, c => c.Permission);

        // The acting user must also hold every granted permission.
        return newTargetPermissions.ClaimsMap.Any(granted => granted.Permission && !actorClaims[granted.ClaimName])
            ? new CustomUsersCanOnlyGrantOwnPermissions()
            : null;
    }

    private static BadRequestError CannotManageError(params OrganizationUserType[] targetTypes) =>
        targetTypes.Contains(OrganizationUserType.Owner)
            ? new OnlyOwnersCanManageOwners()
            : new CustomUsersCannotManageAdminsOrOwners();

    private static bool IsAuthorizedByRole(IOrganizationUserRole? actingUser, OrganizationUserType targetType) =>
        actingUser switch
        {
            { Type: OrganizationUserType.Owner } => true,
            { Type: OrganizationUserType.Admin } => targetType is not OrganizationUserType.Owner,
            { Type: OrganizationUserType.Custom } when actingUser.GetPermissions()?.ManageUsers is true =>
                targetType is OrganizationUserType.User or OrganizationUserType.Custom,
            _ => false
        };

    // Provider users aren't org members but hold Owner-level authority.
    private async Task<bool> IsProviderAsync(Guid actingUserId, Guid organizationId) =>
        (await providerUserRepository.GetManyOrganizationDetailsByUserAsync(actingUserId, ProviderUserStatusType.Confirmed))
        .Any(po => po.OrganizationId == organizationId);
}

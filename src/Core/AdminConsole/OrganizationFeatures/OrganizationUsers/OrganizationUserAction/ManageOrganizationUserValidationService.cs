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
public class ManageOrganizationUserValidationService(
    IProviderUserRepository providerUserRepository,
    IOrganizationUserRepository organizationUserRepository) : IManageOrganizationUserValidationService
{
    public async Task<Error?> CanManageAsync(Guid actingUserId, IOrganizationUserRole? actingUser, IOrganizationUserRole targetUser)
    {
        if (IsAuthorizedByRole(actingUser, targetUser.Type) || await IsProviderAsync(actingUserId, targetUser.OrganizationId))
        {
            return null;
        }

        return new CannotManageTargetUser();
    }

    public async Task<Error?> CanManageRoleChangeAsync(Guid actingUserId, IOrganizationUserRole actingUser,
        IOrganizationUserRole targetUser, OrganizationUserType targetNewUserType, Permissions? targetNewPermissions)
    {
        // Must be able to manage both the current and requested role.
        var authorizedByRole = IsAuthorizedByRole(actingUser, targetUser.Type)
                               && IsAuthorizedByRole(actingUser, targetNewUserType);

        if (!authorizedByRole && !await IsProviderAsync(actingUserId, targetUser.OrganizationId))
        {
            // Only an Owner can manage an Owner; otherwise it's a Custom user reaching above their authority.
            return targetUser.Type == OrganizationUserType.Owner || targetNewUserType == OrganizationUserType.Owner
                ? new OnlyOwnersCanManageOwners()
                : new CustomUsersCannotManageAdminsOrOwners();
        }

        return ValidateCustomPermissionsGrant(actingUser, targetNewUserType, targetNewPermissions);
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
        IOrganizationUserRole actingUser, OrganizationUserType targetNewUserType, Permissions? targetNewPermissions)
    {
        // Owners and Admins can grant any custom permission; the check only applies to a Custom grantor.
        if (targetNewUserType != OrganizationUserType.Custom
            || targetNewPermissions is null
            || actingUser.Type is OrganizationUserType.Owner or OrganizationUserType.Admin)
        {
            return null;
        }

        var actorClaims = (actingUser.GetPermissions() ?? new Permissions())
            .ClaimsMap.ToDictionary(c => c.ClaimName, c => c.Permission);

        // The acting user must also hold every granted permission.
        return targetNewPermissions.ClaimsMap.Any(granted => granted.Permission && !actorClaims[granted.ClaimName])
            ? new CustomUsersCanOnlyGrantOwnPermissions()
            : null;
    }

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

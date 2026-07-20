using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

/// <inheritdoc />
public class ManageOrganizationUserValidationService(IProviderUserRepository providerUserRepository) : IManageOrganizationUserValidationService
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

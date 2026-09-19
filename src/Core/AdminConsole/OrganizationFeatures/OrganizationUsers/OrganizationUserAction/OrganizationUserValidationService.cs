using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

/// <inheritdoc />
public class OrganizationUserValidationService(
    IOrganizationUserRepository organizationUserRepository) : IOrganizationUserValidationService
{
    public Error? ValidateAuthorityOver(IActingUser actingUser, IOrganizationUserRole targetUser)
    {
        // SystemUsers exist outside the organization hierarchy and skip the check.
        if (actingUser is SystemUser)
        {
            return null;
        }

        // Narrow to StandardUsers only - anything else is unhandled and would require an update
        if (actingUser is not StandardUser standardUser)
        {
            throw new ArgumentOutOfRangeException(nameof(actingUser));
        }

        // Providers act with Owner authority, so they can manage anyone.
        if (standardUser.IsProvider)
        {
            return null;
        }

        // A caller who is neither a member nor a provider has no standing to act on members.
        if (standardUser.OrganizationUserType is null)
        {
            return new ActingUserMustBeMemberOrProvider();
        }

        var actingType = standardUser.OrganizationUserType;
        return targetUser.Type switch
        {
            // Only an Owner can manage another Owner.
            OrganizationUserType.Owner
                when actingType is not OrganizationUserType.Owner
                => new OnlyOwnersCanManageOwners(),

            // Owners and Admins can manage Admins.
            OrganizationUserType.Admin
                when actingType is not (OrganizationUserType.Owner or OrganizationUserType.Admin)
                => new CustomUsersCannotManageAdminsOrOwners(),

            // Users and Custom members can be managed by Owners, Admins, or Custom users with ManageUsers.
            OrganizationUserType.User or OrganizationUserType.Custom
                when standardUser is not (
                    { OrganizationUserType: OrganizationUserType.Owner or OrganizationUserType.Admin }
                    or { OrganizationUserType: OrganizationUserType.Custom, Permissions.ManageUsers: true })
                => new CustomUsersCannotManageAdminsOrOwners(),

            // Any actor not rejected above is authorized for this target.
            _ => null
        };
    }

    public Error? ValidateAuthorityForRoleChange(IActingUser actingUser, IOrganizationUserRole targetUser, IOrganizationUserRole newTargetUser) =>
        // Must be able to manage both the current and requested role, and only grant permissions the actor holds.
        ValidateAuthorityOver(actingUser, targetUser)
        ?? ValidateAuthorityOver(actingUser, newTargetUser)
        ?? ValidateCustomPermissionsGrant(actingUser, newTargetUser);

    public async Task<Error?> ValidateFreeOrgAdminLimitAsync(Guid? userId, PlanType planType,
        OrganizationUserType currentUserType, OrganizationUserType newUserType)
    {
        if (planType != PlanType.Free
            || !userId.HasValue // OrgUser is not linked to a User yet, so we can't evaluate their other memberships; this will be enforced on accept/confirm flows instead.
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
        IActingUser actingUser, IOrganizationUserRole newTargetUser)
    {
        var newTargetPermissions = newTargetUser.GetPermissions();

        // The check only applies to a Custom grantor acting as a member. Owners, Admins, providers, and system
        // users can grant any custom permission.
        if (newTargetUser.Type != OrganizationUserType.Custom
            || newTargetPermissions is null
            || actingUser is not StandardUser { IsProvider: false, OrganizationUserType: OrganizationUserType.Custom } customActor)
        {
            return null;
        }

        var actorClaims = (customActor.Permissions ?? new Permissions())
            .ClaimsMap.ToDictionary(c => c.ClaimName, c => c.Permission);

        // The acting user must also hold every granted permission.
        return newTargetPermissions.ClaimsMap.Any(granted => granted.Permission && !actorClaims[granted.ClaimName])
            ? new CustomUsersCanOnlyGrantOwnPermissions()
            : null;
    }
}

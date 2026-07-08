using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

public class UpdateOrganizationUserValidator(
    IOrganizationUserRepository organizationUserRepository,
    IGroupRepository groupRepository,
    IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
    IOrganizationUserValidationService organizationUserValidationService)
    : IUpdateOrganizationUserValidator
{
    public async Task<ValidationResult<UpdateOrganizationUserRequest>> ValidateAsync(
        UpdateOrganizationUserRequest request)
    {
        var organizationUser = request.OrganizationUserToUpdate;

        if (organizationUser.Id == Guid.Empty)
        {
            return Invalid(request, new InviteUserFirst());
        }

        // A user can only be an admin or owner of a single free organization.
        if (!await IsValidFreeOrganizationAdminAsync(organizationUser, request.NewType, request.Organization))
        {
            return Invalid(request, new CannotBeAdminOfMultipleFreeOrgs());
        }

        // When admins are not allowed access to all collections, a user editing themselves cannot add
        // themselves to collections they don't already have access to.
        if (IsAddingSelfToCollection(request))
        {
            return Invalid(request, new CannotAddSelfToCollection());
        }

        var collectionsToSave = request.NewCollections ?? [];
        if (collectionsToSave.Count != 0)
        {
            if (!CollectionsAreValid(collectionsToSave, request.ReferencedCollections, organizationUser.OrganizationId))
            {
                return Invalid(request, new CollectionNotFound());
            }

            if (ContainsDefaultUserCollection(collectionsToSave, request.ReferencedCollections))
            {
                return Invalid(request, new CannotAssignDefaultCollection());
            }
        }

        if (request.NewGroups?.Any() == true)
        {
            var groupAccess = request.NewGroups.ToList();
            var groups = await groupRepository.GetManyByManyIds(groupAccess);
            if (!GroupsAreValid(groupAccess, groups, organizationUser.OrganizationId))
            {
                return Invalid(request, new GroupNotFound());
            }
        }

        var escalationError = await ValidateNotEscalatingAboveOwnRoleAsync(request);
        if (escalationError is not null)
        {
            return Invalid(request, escalationError);
        }

        var grantError = ValidateCustomPermissionsGrant(request);
        if (grantError is not null)
        {
            return Invalid(request, grantError);
        }

        // Custom permissions require an Enterprise plan.
        if (request is { NewType: OrganizationUserType.Custom, Organization.UseCustomPermissions: false })
        {
            return Invalid(request, new CustomPermissionsNotEnabled());
        }

        if (request.NewType != OrganizationUserType.Owner &&
            !await hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId,
                [organizationUser.Id]))
        {
            return Invalid(request, new MustHaveConfirmedOwner());
        }

        if (collectionsToSave.Count > 0 && collectionsToSave.Any(cas => cas.Manage && (cas.ReadOnly || cas.HidePasswords)))
        {
            return Invalid(request, new ManageMutuallyExclusive());
        }

        return Valid(request);
    }

    private async Task<bool> IsValidFreeOrganizationAdminAsync(OrganizationUser organizationUser, OrganizationUserType newType, Organization organization)
    {
        if (organization.PlanType != PlanType.Free)
        {
            return true;
        }

        if (!organizationUser.UserId.HasValue)
        {
            return true;
        }

        if (newType is not (OrganizationUserType.Admin or OrganizationUserType.Owner))
        {
            return true;
        }

        // Free organizations have few users, so the extra query here is acceptable.
        var adminCount = await organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(organizationUser.UserId!.Value);
        var isCurrentAdminOrOwner = organizationUser.Type is OrganizationUserType.Admin or OrganizationUserType.Owner;

        if (isCurrentAdminOrOwner && adminCount <= 1)
        {
            return true;
        }

        if (!isCurrentAdminOrOwner && adminCount == 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Prevents a caller from granting or modifying a role higher than their own. Delegates the authority
    /// decision to <see cref="IOrganizationUserValidationService.CanManage"/>, checking both the member's
    /// current and requested role. System users skip the check; a denial is mapped to a specific error.
    /// </summary>
    private async Task<Error?> ValidateNotEscalatingAboveOwnRoleAsync(UpdateOrganizationUserRequest request)
    {
        if (request.PerformedBy is SystemUser)
        {
            return null;
        }

        var actingUser = request.PerformedByOrganizationUser;
        var actingUserId = request.PerformedBy.UserId!.Value; // non-null: SystemUser already returned above

        // A synthetic member carrying the org id, so the service resolves provider authority against it.
        var requestedRole = new OrganizationUser
        {
            Type = request.NewType,
            OrganizationId = request.OrganizationUserToUpdate.OrganizationId
        };

        var canManageCurrentRole =
            await organizationUserValidationService.CanManage(actingUserId, actingUser, request.OrganizationUserToUpdate) is null;
        var canManageNewRole =
            await organizationUserValidationService.CanManage(actingUserId, actingUser, requestedRole) is null;

        if (canManageCurrentRole && canManageNewRole)
        {
            return null;
        }

        // Only an Owner can manage an Owner; otherwise it's a Custom user reaching above their authority.
        if (request.OrganizationUserToUpdate.Type == OrganizationUserType.Owner || request.NewType == OrganizationUserType.Owner)
        {
            return new OnlyOwnersCanManageOwners();
        }

        return new CustomUsersCannotManageAdminsOrOwners();
    }

    /// <summary>
    /// A Custom caller can only grant another member the custom permissions they themselves hold. Owners,
    /// admins, providers (no membership), and system users are exempt: their authority already exceeds any
    /// grantable custom permission.
    /// </summary>
    private static Error? ValidateCustomPermissionsGrant(UpdateOrganizationUserRequest request)
    {
        if (request.NewType != OrganizationUserType.Custom || request.NewPermissions is null)
        {
            return null;
        }

        // System users and providers (no membership) aren't constrained to a member's own permission set.
        if (request.PerformedBy is SystemUser || request.PerformedByOrganizationUser is null)
        {
            return null;
        }

        if (request.PerformedByOrganizationUser.Type is OrganizationUserType.Owner or OrganizationUserType.Admin)
        {
            return null;
        }

        var actingPermissions = request.PerformedByOrganizationUser.GetPermissions() ?? new Permissions();

        return GrantsPermissionNotHeldByActor(request.NewPermissions, actingPermissions)
            ? new CustomUsersCanOnlyGrantOwnPermissions()
            : null;
    }

    private static bool GrantsPermissionNotHeldByActor(Permissions requested, Permissions actorPermissions)
    {
        var actorClaims = actorPermissions.ClaimsMap.ToDictionary(c => c.ClaimName, c => c.Permission);

        // Every permission being granted must also be held by the acting user.
        return requested.ClaimsMap.Any(granted => granted.Permission && !actorClaims[granted.ClaimName]);
    }

    private static bool IsAddingSelfToCollection(UpdateOrganizationUserRequest request)
    {
        var editingSelf = request.PerformedBy is not SystemUser
                          && request.OrganizationUserToUpdate.UserId.HasValue
                          && request.OrganizationUserToUpdate.UserId == request.PerformedBy.UserId;

        return editingSelf
               && !request.OrganizationAbility.AllowAdminAccessToAllCollectionItems
               && (request.NewCollections ?? []).Any(c => !request.CurrentCollectionsIds.Contains(c.Id));
    }

    private static bool CollectionsAreValid(List<CollectionAccessSelection> collectionAccess,
        ICollection<Collection> collections, Guid organizationId)
    {
        var collectionIds = collections.Select(c => c.Id);

        var missingCollection = collectionAccess.FirstOrDefault(cas => !collectionIds.Contains(cas.Id));

        return missingCollection == null && collections.All(c => c.OrganizationId == organizationId);
    }

    private static bool ContainsDefaultUserCollection(
        List<CollectionAccessSelection> collectionAccess, ICollection<Collection> collections) =>
        collectionAccess
            .Any(cas => collections.Any(c => c.Id == cas.Id && c.Type == CollectionType.DefaultUserCollection));

    private static bool GroupsAreValid(ICollection<Guid> groupAccess, ICollection<Group> groups, Guid organizationId)
    {
        var groupIds = groups.Select(g => g.Id);

        var missingGroupId = groupAccess.FirstOrDefault(gId => !groupIds.Contains(gId));

        return missingGroupId == Guid.Empty && groups.All(g => g.OrganizationId == organizationId);
    }
}

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
using Organization = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

public class UpdateOrganizationUserValidator(
    IOrganizationUserRepository organizationUserRepository,
    ICollectionRepository collectionRepository,
    IGroupRepository groupRepository,
    IHasConfirmedOwnersExceptQuery hasConfirmedOwnersExceptQuery,
    IOrganizationUserValidationService organizationUserValidationService)
    : IUpdateOrganizationUserValidator
{
    public async Task<ValidationResult<UpdateOrganizationUserValidationRequest>> ValidateAsync(
        UpdateOrganizationUserValidationRequest request)
    {
        var organizationUser = request.OrganizationUser;

        // The user must already be invited to the organization.
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

        // All posted collections must exist and belong to the organization. Default user collections are
        // filtered out of the set that is ultimately persisted.
        var collectionsToSave = request.CollectionsToSave;
        if (collectionsToSave.Count != 0)
        {
            var collections = await collectionRepository.GetManyByManyIdsAsync(collectionsToSave.Select(c => c.Id));
            if (!CollectionsAreValid(collectionsToSave, collections, organizationUser.OrganizationId))
            {
                return Invalid(request, new CollectionNotFound());
            }

            collectionsToSave = ExcludeDefaultUserCollections(collectionsToSave, collections);
        }

        // All posted groups must exist and belong to the organization.
        if (request.GroupsToSave?.Any() == true)
        {
            var groupAccess = request.GroupsToSave.ToList();
            var groups = await groupRepository.GetManyByManyIds(groupAccess);
            if (!GroupsAreValid(groupAccess, groups, organizationUser.OrganizationId))
            {
                return Invalid(request, new GroupNotFound());
            }
        }

        // A caller cannot grant a role higher than their own. Mirrors v1's
        // OrganizationService.ValidateOrganizationUserUpdatePermissions escalation guard.
        var escalationError = ValidateNotEscalatingAboveOwnRole(request);
        if (escalationError is not null)
        {
            return Invalid(request, escalationError);
        }

        // Custom permissions require an Enterprise plan.
        if (request.NewType == OrganizationUserType.Custom && !request.Organization.UseCustomPermissions)
        {
            return Invalid(request, new CustomPermissionsNotEnabled());
        }

        // The organization must retain at least one confirmed owner after the update.
        if (request.NewType != OrganizationUserType.Owner &&
            !await hasConfirmedOwnersExceptQuery.HasConfirmedOwnersExceptAsync(organizationUser.OrganizationId,
                [organizationUser.Id]))
        {
            return Invalid(request, new MustHaveConfirmedOwner());
        }

        // The Manage permission is mutually exclusive with ReadOnly and HidePasswords.
        if (collectionsToSave.Count > 0 && collectionsToSave.Any(cas => cas.Manage && (cas.ReadOnly || cas.HidePasswords)))
        {
            return Invalid(request, new ManageMutuallyExclusive());
        }

        return Valid(request with { CollectionsToSave = collectionsToSave });
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

        // Since free organizations only support a few users there is not much point in avoiding N+1 queries for this.
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
    /// Prevents a caller from granting a role higher than their own (privilege escalation). Delegates the
    /// authority decision to the shared <see cref="IOrganizationUserValidationService.CanManage"/> rules,
    /// checking both the member's current role and the requested role so an existing higher-ranked member
    /// cannot be modified from below either. System users act with full authority (v1 gated this on a real
    /// acting user), so the check is skipped for them. The v1-specific error messages are preserved by
    /// mapping the denial back to which role was out of reach.
    /// </summary>
    private Error? ValidateNotEscalatingAboveOwnRole(UpdateOrganizationUserValidationRequest request)
    {
        if (request.PerformedBy is SystemUser)
        {
            return null;
        }

        var actingUser = request.PerformedByOrganizationUser;
        var isProvider = request.PerformedBy.IsOrganizationOwnerOrProvider;

        var canManageCurrentRole =
            organizationUserValidationService.CanManage(actingUser, request.OrganizationUser, isProvider) is null;
        var canManageNewRole =
            organizationUserValidationService.CanManage(actingUser, new OrganizationUser { Type = request.NewType }, isProvider) is null;

        if (canManageCurrentRole && canManageNewRole)
        {
            return null;
        }

        // Map the denial to v1's specific message. An Owner (current or requested) can only be managed by an
        // Owner; everything else that trips the check is a Custom user reaching above their authority.
        if (request.OrganizationUser.Type == OrganizationUserType.Owner || request.NewType == OrganizationUserType.Owner)
        {
            return new OnlyOwnersCanManageOwners();
        }

        return new CustomUsersCannotManageAdminsOrOwners();
    }

    private static bool IsAddingSelfToCollection(UpdateOrganizationUserValidationRequest request)
    {
        var editingSelf = request.PerformedBy is not SystemUser
                          && request.OrganizationUser.UserId.HasValue
                          && request.OrganizationUser.UserId == request.PerformedBy.UserId;

        return editingSelf
               && !request.OrganizationAbility.AllowAdminAccessToAllCollectionItems
               && request.CollectionsToSave.Any(c => !request.CurrentAccessIds.Contains(c.Id));
    }

    private static bool CollectionsAreValid(List<CollectionAccessSelection> collectionAccess,
        ICollection<Collection> collections, Guid organizationId)
    {
        var collectionIds = collections.Select(c => c.Id);

        var missingCollection = collectionAccess.FirstOrDefault(cas => !collectionIds.Contains(cas.Id));

        return missingCollection == null && collections.All(c => c.OrganizationId == organizationId);
    }

    private static List<CollectionAccessSelection> ExcludeDefaultUserCollections(
        List<CollectionAccessSelection> collectionAccess, ICollection<Collection> collections) =>
        collectionAccess
            .Where(cas => collections.Any(c => c.Id == cas.Id && c.Type != CollectionType.DefaultUserCollection))
            .ToList();

    private static bool GroupsAreValid(ICollection<Guid> groupAccess, ICollection<Group> groups, Guid organizationId)
    {
        var groupIds = groups.Select(g => g.Id);

        var missingGroupId = groupAccess.FirstOrDefault(gId => !groupIds.Contains(gId));

        return missingGroupId == Guid.Empty && groups.All(g => g.OrganizationId == organizationId);
    }
}

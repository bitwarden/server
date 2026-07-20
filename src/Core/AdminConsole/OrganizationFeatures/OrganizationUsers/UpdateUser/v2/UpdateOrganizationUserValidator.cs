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
    IManageOrganizationUserValidationService manageOrganizationUserValidationService)
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

        var roleChangeError = await ValidateRoleChangeAsync(request);
        if (roleChangeError is not null)
        {
            return Invalid(request, roleChangeError);
        }

        // Ensure the organization's plan supports custom permissions.
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

        if (collectionsToSave.Count > 0 && collectionsToSave.Any(cas => !cas.Valid()))
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
    /// Delegates the role-change authority decision to
    /// <see cref="IManageOrganizationUserValidationService.CanManageRoleChangeAsync"/>. System users skip the check.
    /// </summary>
    private async Task<Error?> ValidateRoleChangeAsync(UpdateOrganizationUserRequest request)
    {
        if (request.PerformedBy is not StandardUser standardUser)
        {
            return null;
        }

        var actingUser = new OrganizationUserRole(
            standardUser.OrganizationUserType!.Value,
            request.OrganizationUserToUpdate.OrganizationId,
            standardUser.Permissions);

        return await manageOrganizationUserValidationService.CanManageRoleChangeAsync(
            standardUser.UserId!.Value,
            actingUser,
            request.OrganizationUserToUpdate,
            request.NewType,
            request.NewPermissions);
    }

    private static bool IsAddingSelfToCollection(UpdateOrganizationUserRequest request)
    {
        var editingSelf = request.PerformedBy is not SystemUser
                          && request.OrganizationUserToUpdate.UserId.HasValue
                          && request.OrganizationUserToUpdate.UserId == request.PerformedBy.UserId;

        return editingSelf
               && !request.Organization.AllowAdminAccessToAllCollectionItems
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

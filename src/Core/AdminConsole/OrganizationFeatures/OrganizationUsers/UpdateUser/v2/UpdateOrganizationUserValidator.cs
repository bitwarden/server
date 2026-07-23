using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

public class UpdateOrganizationUserValidator(
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

        var freeOrgAdminError = await organizationUserValidationService.ValidateFreeOrgAdminLimitAsync(
            organizationUser.UserId, request.Organization.PlanType, organizationUser.Type, request.NewType);
        if (freeOrgAdminError is not null)
        {
            return Invalid(request, freeOrgAdminError);
        }

        var collectionsToSave = request.CollectionsToSave ?? [];
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

    /// <summary>
    /// Delegates the role-change authority decision to
    /// <see cref="IOrganizationUserValidationService.CanManageRoleChangeAsync"/>. System users skip the check.
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

        return await organizationUserValidationService.CanManageRoleChangeAsync(
            standardUser.UserId!.Value,
            actingUser,
            request.OrganizationUserToUpdate,
            request.NewType,
            request.NewPermissions);
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

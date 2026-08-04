using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;

public class ModifyCollectionGroupAccessValidator(IGroupRepository groupRepository)
    : IModifyCollectionGroupAccessValidator
{
    public async Task<ValidationResult<ModifyCollectionGroupAccessRequest>> ValidateAsync(
        ModifyCollectionGroupAccessRequest request)
    {
        if (HasDuplicateIds(request.Add) || HasDuplicateIds(request.Update))
        {
            return Invalid(request, new DuplicateGroupId());
        }

        var addIds = request.Add.Select(a => a.Id).ToHashSet();
        var updateIds = request.Update.Select(u => u.Id).ToHashSet();
        var removeIds = request.Remove.ToHashSet();

        if (addIds.Overlaps(updateIds) || addIds.Overlaps(removeIds) || updateIds.Overlaps(removeIds))
        {
            return Invalid(request, new OverlappingGroupId());
        }

        if (request.Add.Concat(request.Update).Any(s => s.Manage && (s.ReadOnly || s.HidePasswords)))
        {
            return Invalid(request, new InvalidManageAssociation());
        }

        if (request.Targets.Any(t => t.Collection.Type == CollectionType.DefaultUserCollection))
        {
            return Invalid(request, new CannotModifyDefaultUserCollectionAccess());
        }

        // Only meaningful for a single collection: across several, a group may already have access to one
        // target but not another.
        if (request.Targets.Count == 1)
        {
            var existingIds = request.Targets.Single().AccessDetails.Groups.Select(g => g.Id).ToHashSet();
            if (addIds.Any(existingIds.Contains))
            {
                return Invalid(request, new GroupAlreadyHasAccess());
            }

            if (updateIds.Any(id => !existingIds.Contains(id)))
            {
                return Invalid(request, new GroupDoesNotHaveAccess());
            }
        }

        var upsertIds = addIds.Concat(updateIds).ToList();
        if (upsertIds.Count > 0)
        {
            var organizationId = request.Targets.First().Collection.OrganizationId;
            var groups = await groupRepository.GetManyByManyIds(upsertIds);
            if (groups.Count != upsertIds.Count)
            {
                return Invalid(request, new GroupsNotFound());
            }

            if (groups.Any(g => g.OrganizationId != organizationId))
            {
                return Invalid(request, new GroupsNotInOrganization());
            }
        }

        if (!request.AllowAdminAccessToAllCollectionItems
            && request.Targets.Any(t => !HasRemainingManageAccess(t, request, removeIds)))
        {
            return Invalid(request, new NoRemainingManageAccess());
        }

        return Valid(request);
    }

    private static bool HasRemainingManageAccess(
        CollectionGroupAccessTarget target, ModifyCollectionGroupAccessRequest request, HashSet<Guid> removeIds)
    {
        if (target.AccessDetails.Users.Any(u => u.Manage))
        {
            return true;
        }

        var existingIds = target.AccessDetails.Groups.Select(g => g.Id).ToHashSet();
        var updatedById = request.Update.ToDictionary(u => u.Id);
        var finalGroups = target.AccessDetails.Groups
            .Where(g => !removeIds.Contains(g.Id))
            .Select(g => updatedById.GetValueOrDefault(g.Id, g))
            .Concat(request.Add)
            // An Update entry grants access on targets the group isn't a member of, so it counts as an Add here.
            .Concat(request.Update.Where(u => !existingIds.Contains(u.Id)));

        return finalGroups.Any(g => g.Manage);
    }

    private static bool HasDuplicateIds(IReadOnlyCollection<CollectionAccessSelection> selections)
    {
        var ids = selections.Select(s => s.Id).ToList();
        return ids.Count != ids.Distinct().Count();
    }
}

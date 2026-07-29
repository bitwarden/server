using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;

public class ModifyCollectionUserAccessValidator(IOrganizationUserRepository organizationUserRepository)
    : IModifyCollectionUserAccessValidator
{
    public async Task<ValidationResult<ModifyCollectionUserAccessRequest>> ValidateAsync(
        ModifyCollectionUserAccessRequest request)
    {
        if (HasDuplicateIds(request.Add) || HasDuplicateIds(request.Update))
        {
            return Invalid(request, new DuplicateOrganizationUserId());
        }

        var addIds = request.Add.Select(a => a.Id).ToHashSet();
        var updateIds = request.Update.Select(u => u.Id).ToHashSet();
        var removeIds = request.Remove.ToHashSet();

        if (addIds.Overlaps(updateIds) || addIds.Overlaps(removeIds) || updateIds.Overlaps(removeIds))
        {
            return Invalid(request, new OverlappingOrganizationUserId());
        }

        if (request.Add.Concat(request.Update).Any(s => s.Manage && (s.ReadOnly || s.HidePasswords)))
        {
            return Invalid(request, new InvalidManageAssociation());
        }

        if (request.Targets.Any(t => t.Collection.Type == CollectionType.DefaultUserCollection))
        {
            return Invalid(request, new CannotModifyDefaultUserCollectionAccess());
        }

        // This check only makes sense for one collection. With multiple collections, a user might already
        // have access to one but not another, so we skip it.
        if (request.Targets.Count == 1)
        {
            var existingIds = request.Targets.Single().AccessDetails.Users.Select(u => u.Id).ToHashSet();
            if (addIds.Any(existingIds.Contains))
            {
                return Invalid(request, new OrganizationUserAlreadyHasAccess());
            }

            if (updateIds.Any(id => !existingIds.Contains(id)))
            {
                return Invalid(request, new OrganizationUserDoesNotHaveAccess());
            }
        }

        var upsertIds = addIds.Concat(updateIds).ToList();
        if (upsertIds.Count > 0)
        {
            var organizationId = request.Targets.First().Collection.OrganizationId;
            var organizationUsers = await organizationUserRepository.GetManyAsync(upsertIds);
            if (organizationUsers.Count != upsertIds.Count)
            {
                return Invalid(request, new OrganizationUsersNotFound());
            }

            if (organizationUsers.Any(ou => ou.OrganizationId != organizationId))
            {
                return Invalid(request, new OrganizationUsersNotInOrganization());
            }
        }

        // Checks Add and Update, since putting yourself in Update instead of Add still grants new access.
        // Only blocks joining a collection you're not in yet. Raising access on one you're already in is fine,
        // since the authorization layer already requires you to manage that collection first.
        if (request.PerformingOrganizationUserId is { } performingId
            && (addIds.Contains(performingId) || updateIds.Contains(performingId))
            && !request.AllowAdminAccessToAllCollectionItems
            && request.Targets.Any(t => !t.AccessDetails.Users.Any(u => u.Id == performingId)))
        {
            return Invalid(request, new CannotAddSelfToCollection());
        }

        if (!request.AllowAdminAccessToAllCollectionItems
            && request.Targets.Any(t => !HasRemainingManageAccess(t, request, removeIds)))
        {
            return Invalid(request, new NoRemainingManageAccess());
        }

        return Valid(request);
    }

    private static bool HasRemainingManageAccess(
        CollectionUserAccessTarget target, ModifyCollectionUserAccessRequest request, HashSet<Guid> removeIds)
    {
        if (target.AccessDetails.Groups.Any(g => g.Manage))
        {
            return true;
        }

        var existingIds = target.AccessDetails.Users.Select(u => u.Id).ToHashSet();
        var updatedById = request.Update.ToDictionary(u => u.Id);
        var finalUsers = target.AccessDetails.Users
            .Where(u => !removeIds.Contains(u.Id))
            .Select(u => updatedById.GetValueOrDefault(u.Id, u))
            .Concat(request.Add)
            // An Update entry still grants access on any target where that user isn't already a member,
            // so it counts the same as Add here.
            .Concat(request.Update.Where(u => !existingIds.Contains(u.Id)));

        return finalUsers.Any(u => u.Manage);
    }

    private static bool HasDuplicateIds(IReadOnlyCollection<CollectionAccessSelection> selections)
    {
        var ids = selections.Select(s => s.Id).ToList();
        return ids.Count != ids.Distinct().Count();
    }
}

using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class CollectionGroupAuthorizationHandler : BulkAuthorizationHandler<CollectionGroupOperationRequirement, CollectionGroup>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IGroupRepository _groupRepository;

    public CollectionGroupAuthorizationHandler(
        ICurrentContext currentContext,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _groupRepository = groupRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionGroupOperationRequirement requirement,
        ICollection<CollectionGroup> resources)
    {
        switch (requirement)
        {
            case not null when requirement == CollectionGroupOperation.Create:
                await CanCreateAsync(context, requirement, resources);
                break;
            case not null when requirement == CollectionGroupOperation.Delete:
                await CanDeleteAsync(context, requirement, resources);
                break;
            case not null when requirement.Name == nameof(CollectionGroupOperation.CreateForNewCollection):
                await CanCreateForNewCollectionAsync(context, requirement, resources);
                break;
        }
    }

    /// <summary>
    /// Ensure the acting user can create the requested <see cref="CollectionGroup"/> resource(s).
    /// </summary>
    /// <remarks>
    /// Checks that the following are all true:
    /// - The target collection(s) exists
    /// - The acting user is an owner/admin AND the collection management setting is enabled
    ///   OR
    ///   The target collection(s) is manageable by the acting user.
    /// - The target group(s) exists and belongs to the same organization as the target collection
    /// </remarks>
    private async Task CanCreateAsync(AuthorizationHandlerContext context, CollectionGroupOperationRequirement requirement, ICollection<CollectionGroup> resources)
    {
        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        var distinctTargetCollectionIds = resources.Select(c => c.CollectionId).Distinct().ToList();

        // List of collections the user is performing the operation on
        var targetCollections = await _collectionRepository.GetManyByManyIdsAsync(distinctTargetCollectionIds);

        // A target collection does not exist, fail the requirement
        if (targetCollections.Count != distinctTargetCollectionIds.Count)
        {
            context.Fail();
            return;
        }

        // TODO: Add check for future organization Collection Management setting here and skip the next check if it's enabled

        // List of collections the user is allowed to manage
        var manageableCollections = (await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value)).Where(c => c.Manage).ToList();

        // Any target collection is not in the list of manageable collections, fail the requirement
        if (targetCollections.Any(tc => !manageableCollections.Exists(c => c.Id == tc.Id)))
        {
            context.Fail();
            return;
        }

        var distinctTargetGroupIds = resources.Select(c => c.GroupId).Distinct().ToList();

        // List of groups the user is performing the operation on
        var targetGroups = await _groupRepository.GetManyByManyIds(distinctTargetGroupIds);

        // A target group does not exist, fail the requirement
        if (targetGroups.Count != distinctTargetGroupIds.Count)
        {
            context.Fail();
            return;
        }

        foreach (var targetGroup in targetGroups)
        {
            var targetCollectionsForGroup = from c in targetCollections
                                            join cg in resources on c.Id equals cg.CollectionId
                                            where cg.GroupId == targetGroup.Id
                                            select c;

            // Target group is not in the same organization as the target collection its being assigned to, fail the requirement
            if (targetCollectionsForGroup.Any(tc => tc.OrganizationId != targetGroup.OrganizationId))
            {
                context.Fail();
                return;
            }
        }

        context.Succeed(requirement);
    }

    /// <summary>
    /// Ensure the acting user can created the requested <see cref="CollectionGroup"/> resource(s) for the
    /// specified collection (that has not be created in the database yet).
    /// </summary>
    /// <remarks>
    /// Checks that the following are all true:
    /// - The target collection is provided in the requirement
    /// - All collection groups are assigned to the target collection in the requirement
    /// - The target groups exist and belong to the same organization as the target collection
    /// </remarks>
    private async Task CanCreateForNewCollectionAsync(AuthorizationHandlerContext context, CollectionGroupOperationRequirement requirement, ICollection<CollectionGroup> resources)
    {
        // Without the target collection, we can't check anything else
        if (requirement.Collection == null)
        {
            context.Fail();
            return;
        }

        // All collection users must be assigned to the target collection in the requirement, otherwise fail
        if (resources.Any(cu => cu.CollectionId != requirement.Collection.Id))
        {
            context.Fail();
            return;
        }

        var distinctTargetGroupIds = resources.Select(c => c.GroupId).Distinct().ToList();

        // List of groups the user is performing the operation on
        var targetGroups = await _groupRepository.GetManyByManyIds(distinctTargetGroupIds);

        // A target group does not exist, fail the requirement
        if (targetGroups.Count != distinctTargetGroupIds.Count)
        {
            context.Fail();
            return;
        }

        // If any target groups belong to a different organization than the target collection, fail the requirement
        if (targetGroups.Any(tu => tu.OrganizationId != requirement.Collection.OrganizationId))
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }

    /// <summary>
    /// Ensure the acting user can delete the requested <see cref="CollectionGroup"/> resource(s).
    /// </summary>
    /// <remarks>
    /// Checks that the following are all true:
    /// - The target collection(s) exists
    /// - The acting user is an owner/admin AND the collection management setting is enabled
    ///   OR
    ///   The target collection(s) is manageable by the acting user.
    /// </remarks>
    private async Task CanDeleteAsync(AuthorizationHandlerContext context, CollectionGroupOperationRequirement requirement, ICollection<CollectionGroup> resources)
    {
        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        var distinctTargetCollectionIds = resources.Select(c => c.CollectionId).Distinct().ToList();

        // List of collections the user is performing the operation on
        var targetCollections = await _collectionRepository.GetManyByManyIdsAsync(distinctTargetCollectionIds);

        // A target collection does not exist, fail the requirement
        if (targetCollections.Count != distinctTargetCollectionIds.Count)
        {
            context.Fail();
            return;
        }

        // TODO: Add check for future organization Collection Management setting here and skip the next check if it's enabled

        // List of collections the user is allowed to manage
        var manageableCollections = (await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value)).Where(c => c.Manage).ToList();

        // Any target collection is not in the list of manageable collections, fail the requirement
        if (targetCollections.Any(tc => !manageableCollections.Exists(c => c.Id == tc.Id)))
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }
}

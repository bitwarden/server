using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class CollectionUserAuthorizationHandler : BulkAuthorizationHandler<CollectionUserOperationRequirement, CollectionUser>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public CollectionUserAuthorizationHandler(ICurrentContext currentContext, ICollectionRepository collectionRepository, IOrganizationUserRepository organizationUserRepository)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _organizationUserRepository = organizationUserRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionUserOperationRequirement requirement,
        ICollection<CollectionUser> resources)
    {
        switch (requirement)
        {
            case not null when requirement == CollectionUserOperation.Create:
                await CanCreateAsync(context, requirement, resources);
                break;
            case not null when requirement == CollectionUserOperation.Delete:
                await CanDeleteAsync(context, requirement, resources);
                break;
        }
    }

    /// <summary>
    /// Ensure the acting user can create the requested <see cref="CollectionUser"/> resource(s).
    /// </summary>
    /// <remarks>
    /// Checks that the following are all true:
    /// - The target collection(s) exists 
    /// - The acting user is an owner/admin AND the collection management setting is enabled
    ///   OR
    ///   The target collection(s) is manageable by the acting user. 
    /// - The target user(s) exists and belongs to the same organization as the target collection
    /// </remarks>
    private async Task CanCreateAsync(AuthorizationHandlerContext context,
        CollectionUserOperationRequirement requirement, ICollection<CollectionUser> resource)
    {
        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        var distinctTargetCollectionIds = resource.Select(c => c.CollectionId).Distinct().ToList();

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

        var distinctTargetUserIds = resource.Select(c => c.OrganizationUserId).Distinct().ToList();

        // List of users the user is performing the operation on
        var targetUsers = await _organizationUserRepository.GetManyAsync(distinctTargetUserIds);

        // A target user does not exist, fail the requirement
        if (targetUsers.Count != distinctTargetUserIds.Count)
        {
            context.Fail();
            return;
        }

        foreach (var targetUser in targetUsers)
        {
            var targetCollectionsForUser = from c in targetCollections
                                           join cu in resource on c.Id equals cu.CollectionId
                                           where cu.OrganizationUserId == targetUser.Id
                                           select c;

            // Target user is not in the same org as a collection they're being assigned to, fail the requirement
            if (targetCollectionsForUser.Any(tc => tc.OrganizationId != targetUser.OrganizationId))
            {
                context.Fail();
                return;
            }
        }

        context.Succeed(requirement);
    }

    /// <summary>
    /// Ensure the acting user can delete the requested <see cref="CollectionUser"/> resource(s).
    /// </summary>
    /// <remarks>
    /// Checks that the following are all true:
    /// - The target collection(s) exists 
    /// - The acting user is an owner/admin AND the collection management setting is enabled
    ///   OR
    ///   The target collection(s) is manageable by the acting user. 
    /// </remarks>
    private async Task CanDeleteAsync(AuthorizationHandlerContext context,
        CollectionUserOperationRequirement requirement, ICollection<CollectionUser> resource)
    {
        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        var distinctTargetCollectionIds = resource.Select(c => c.CollectionId).Distinct().ToList();

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

using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
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
    private async Task CanCreateAsync(AuthorizationHandlerContext context,
        CollectionUserOperationRequirement requirement, ICollection<CollectionUser> resource)
    {
        await CanManageCollectionAccessAsync(context, requirement, resource);
    }

    /// <summary>
    /// Ensure the acting user can delete the requested <see cref="CollectionUser"/> resource(s).
    /// </summary>
    private async Task CanDeleteAsync(AuthorizationHandlerContext context,
        CollectionUserOperationRequirement requirement, ICollection<CollectionUser> resource)
    {
        await CanManageCollectionAccessAsync(context, requirement, resource);
    }

    /// <summary>
    /// Ensures the acting user is allowed to manage access permissions for the target collections.
    /// </summary>
    private async Task CanManageCollectionAccessAsync(AuthorizationHandlerContext context, CollectionUserOperationRequirement requirement, ICollection<CollectionUser> resource)
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

        var userOgs = await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, _currentContext.UserId.Value);
        var distinctTargetOrganizationIds = targetCollections.Select(tc => tc.OrganizationId).Distinct().ToList();
        var restrictedOrganizations = new List<CurrentContentOrganization>();

        foreach (var orgId in distinctTargetOrganizationIds)
        {
            var org = userOgs.FirstOrDefault(o => orgId == o.Id);

            // Acting user is not a member of the target organization, fail
            if (org == null)
            {
                context.Fail();
                return;
            }

            // Owner/Admins or users with EditAnyCollection permission can always manage collection access
            if (
                org.Permissions.EditAnyCollection ||
                org.Type is OrganizationUserType.Admin or OrganizationUserType.Owner)
            {
                continue;
            }

            restrictedOrganizations.Add(org);
        }

        // All target collections belong to organizations the acting user is allowed to manage collection access, succeed
        if (restrictedOrganizations.Count == 0)
        {
            context.Succeed(requirement);
            return;
        }

        // List of collections the acting user is allowed to manage
        var manageableCollections =
            (await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value))
            .Where(c => c.Manage).ToList();

        foreach (var org in restrictedOrganizations)
        {
            // User must have explicit "Manage" permission on the target collections for this organization
            foreach (var targetCollection in targetCollections.Where(tc => tc.OrganizationId == org.Id))
            {
                // Target collection is not in the list of manageable collections, fail
                if (!manageableCollections.Exists(c => c.Id == targetCollection.Id))
                {
                    context.Fail();
                    return;
                }
            }
        }

        context.Succeed(requirement);
    }
}

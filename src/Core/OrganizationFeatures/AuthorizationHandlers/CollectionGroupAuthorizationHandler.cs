using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class CollectionGroupAuthorizationHandler : CollectionAccessAuthorizationHandlerBase<CollectionGroupOperationRequirement, CollectionGroup>
{
    public CollectionGroupAuthorizationHandler(ICurrentContext currentContext, ICollectionRepository collectionRepository, IOrganizationUserRepository organizationUserRepository) : base(currentContext, collectionRepository, organizationUserRepository)
    {
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
        }
    }

    /// <summary>
    /// Ensure the acting user can create the requested <see cref="CollectionGroup"/> resource(s).
    /// </summary>
    private async Task CanCreateAsync(AuthorizationHandlerContext context,
        CollectionGroupOperationRequirement requirement, IEnumerable<CollectionGroup> resource)
    {
        await CanManageCollectionAccessAsync(context, requirement, resource.Select(cu => cu.CollectionId));
    }

    /// <summary>
    /// Ensure the acting user can delete the requested <see cref="CollectionGroup"/> resource(s).
    /// </summary>
    private async Task CanDeleteAsync(AuthorizationHandlerContext context,
        CollectionGroupOperationRequirement requirement, IEnumerable<CollectionGroup> resource)
    {
        await CanManageCollectionAccessAsync(context, requirement, resource.Select(cu => cu.CollectionId));
    }
}

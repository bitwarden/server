using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class CollectionUserAuthorizationHandler : CollectionAccessAuthorizationHandlerBase<CollectionUserOperationRequirement, CollectionUser>
{
    public CollectionUserAuthorizationHandler(ICurrentContext currentContext, ICollectionRepository collectionRepository, IOrganizationUserRepository organizationUserRepository) : base(currentContext, collectionRepository, organizationUserRepository)
    {
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
        CollectionUserOperationRequirement requirement, IEnumerable<CollectionUser> resource)
    {
        await CanManageCollectionAccessAsync(context, requirement, resource.Select(cu => cu.CollectionId));
    }

    /// <summary>
    /// Ensure the acting user can delete the requested <see cref="CollectionUser"/> resource(s).
    /// </summary>
    private async Task CanDeleteAsync(AuthorizationHandlerContext context,
        CollectionUserOperationRequirement requirement, IEnumerable<CollectionUser> resource)
    {
        await CanManageCollectionAccessAsync(context, requirement, resource.Select(cu => cu.CollectionId));
    }
}

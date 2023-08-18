#nullable enable

using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.Vault.AuthorizationHandlers.Collections;

internal class CollectionAuthorizationHandler : AuthorizationHandler<CollectionOperationRequirement, Collection>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;

    public CollectionAuthorizationHandler(ICurrentContext currentContext, ICollectionRepository collectionRepository)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionOperationRequirement requirement,
        Collection resource)
    {
        switch (requirement)
        {
            case not null when requirement == CollectionOperations.Create:
                await CanCreateAsync(context, requirement, resource);
                break;

            case not null when requirement == CollectionOperations.Delete:
                await CanDeleteAsync(context, requirement, resource);
                break;
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        Collection resource)
    {
        // Use Thomas' helper once his Group resource PR is merged
        var org = _currentContext.Organizations.Find(o => o.Id == resource.OrganizationId);
        if (org == null)
        {
            context.Fail();
            return;
        }

        var success = !org.LimitCollectionCdOwnerAdmin || (org.Type == OrganizationUserType.Owner ||
                                                           org.Type == OrganizationUserType.Admin ||
                                                           org.Permissions.CreateNewCollections ||
                                                           org.Permissions.EditAnyCollection ||
                                                           await _currentContext.ProviderUserForOrgAsync(org.Id));
        if (success)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanDeleteAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        Collection resource)
    {
        // Use Thomas' helper once his Group resource PR is merged
        var org = _currentContext.Organizations.Find(o => o.Id == resource.OrganizationId);
        if (org == null)
        {
            context.Fail();
            return;
        }

        var collectionDetails = await _collectionRepository.GetByIdAsync(resource.Id, _currentContext.UserId.Value);
        var canManageCollection = collectionDetails.Manage;

        var success = canManageCollection;
        if (!canManageCollection)
        {
            success = org.LimitCollectionCdOwnerAdmin &&
                      (org.Type == OrganizationUserType.Owner ||
                      org.Type == OrganizationUserType.Admin ||
                      org.Permissions.DeleteAnyCollection ||
                      await _currentContext.ProviderUserForOrgAsync(org.Id));
        }
        
        if (success)
        {
            context.Succeed(requirement);
        }
    }
}

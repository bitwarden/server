using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.AuthorizationHandlers;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

internal class CollectionAuthorizationHandler : BulkAuthorizationHandler<CollectionOperationRequirement, Collection>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;

    public CollectionAuthorizationHandler(ICurrentContext currentContext, ICollectionRepository collectionRepository)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
    }
    
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionOperationRequirement requirement, ICollection<Collection> resources)
    {
        switch (requirement)
        {
            case not null when requirement == CollectionOperations.Create:
                await CanCreateAsync(context, requirement, resources);
                break;
            
            case not null when requirement == CollectionOperations.Delete:
                await CanDeleteAsync(context, requirement, resources);
                break;
        }
    }
    
    private async Task CanCreateAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement, ICollection<Collection> resources)
    {
        // Bulk creation of collections is not supported
        if (resources.Count > 1)
        {
            throw new NotSupportedException();
        }
        
        // Acting user is not a member of the target organization, fail
        var org = _currentContext.GetOrganization(resources.First().OrganizationId);
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
        ICollection<Collection> resources)
    {
        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        var targetOrganizationId = resources.First().OrganizationId;
        
        // Ensure all target collections belong to the same organization
        if (resources.Any(tc => tc.OrganizationId != targetOrganizationId))
        {
            throw new BadRequestException("Requested collections must belong to the same organization.");
        }
        
        // Acting user is not a member of the target organization, fail
        var org = _currentContext.GetOrganization(targetOrganizationId);
        if (org == null)
        {
            context.Fail();
            return;
        }
        
        // Owners, Admins, Providers, and users with DeleteAnyCollection or EditAnyCollection permission can always delete collections
        if (
            org.Permissions is { DeleteAnyCollection: true, EditAnyCollection: true } ||
            org.Type is OrganizationUserType.Admin or OrganizationUserType.Owner ||
            await _currentContext.ProviderUserForOrgAsync(org.Id))
        {
            context.Succeed(requirement);
            return;
        }
        
        // Other members types should have the Manage capability for all collections being deleted
        if (!org.LimitCollectionCdOwnerAdmin)
        {
            var manageableCollectionIds =
                (await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value))
                .Where(c => c.Manage && c.OrganizationId == org.Id)
                .Select(c => c.Id)
                .ToHashSet();

            // The acting user does not have permission to manage all target collections, fail
            if (resources.Any(c => !manageableCollectionIds.Contains(c.Id)))
            {
                context.Fail();
                return;
            }
        }
        else
        {
            context.Fail();
            return; 
        }

        context.Succeed(requirement);
    }
}

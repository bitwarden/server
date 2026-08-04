#nullable enable
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// Checks whether the caller can change one or more groups' access to one or more collections.
/// All the collections must be in the same organization.
/// </summary>
public class CollectionGroupAuthorizationHandler
    : BulkAuthorizationHandler<CollectionGroupOperationRequirement, CollectionGroupAccessResource>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;
    private HashSet<Guid>? _managedCollectionIds;

    public CollectionGroupAuthorizationHandler(
        ICurrentContext currentContext,
        ICollectionRepository collectionRepository,
        IOrganizationAbilityCacheService organizationAbilityCacheService)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _organizationAbilityCacheService = organizationAbilityCacheService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionGroupOperationRequirement requirement, ICollection<CollectionGroupAccessResource> resources)
    {
        if (resources.Count == 0)
        {
            return;
        }

        if (!_currentContext.UserId.HasValue)
        {
            return;
        }

        var organizationId = resources.First().Collection.OrganizationId;
        if (resources.Any(r => r.Collection.OrganizationId != organizationId))
        {
            throw new BadRequestException("Requested collections must belong to the same organization.");
        }

        var organization = _currentContext.GetOrganization(organizationId);
        var organizationAbility = await _organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        var allowAdminAccessToAllCollectionItems = organizationAbility is { AllowAdminAccessToAllCollectionItems: true };

        var authorized = true;
        foreach (var resource in resources)
        {
            var callerManagesCollection = await CallerManagesCollectionAsync(resource.Collection.Id);
            if (!CollectionGroupAuthorizationRules.CanModifyGroupAccess(
                resource.AccessDetails, organization, allowAdminAccessToAllCollectionItems, callerManagesCollection))
            {
                authorized = false;
                break;
            }
        }

        if (!authorized)
        {
            authorized = await _currentContext.ProviderUserForOrgAsync(organizationId);
        }

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CallerManagesCollectionAsync(Guid collectionId)
    {
        if (_managedCollectionIds == null)
        {
            var callerCollections = await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId!.Value);
            _managedCollectionIds = callerCollections.Where(c => c.Manage).Select(c => c.Id).ToHashSet();
        }

        return _managedCollectionIds.Contains(collectionId);
    }
}

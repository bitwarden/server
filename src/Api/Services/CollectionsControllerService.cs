using System.Security.Claims;
using Bit.Api.Models.Response;
using Bit.Api.Utilities;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Api.Services;

public class CollectionsControllerService : ICollectionsControllerService
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICollectionService _collectionService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentContext _currentContext;
    private readonly IApplicationCacheService _applicationCacheService;

    public CollectionsControllerService(
        ICollectionRepository collectionRepository,
        ICollectionService collectionService,
        IAuthorizationService authorizationService,
        ICurrentContext currentContext,
        IApplicationCacheService applicationCacheService)
    {
        _collectionRepository = collectionRepository;
        _collectionService = collectionService;
        _authorizationService = authorizationService;
        _currentContext = currentContext;
        _applicationCacheService = applicationCacheService;
    }

    public async Task<IEnumerable<CollectionResponseModel>> GetOrganizationCollections(ClaimsPrincipal user, Guid orgId)
    {
        if (await FlexibleCollectionsIsEnabledAsync(orgId))
        {
            // New flexible collections logic
            return await GetByOrgId_vNext(user, orgId);
        }

        // Old pre-flexible collections logic follows
        IEnumerable<Collection> orgCollections = null;
        if (await _currentContext.ManageGroups(orgId))
        {
            // ManageGroups users need to see all collections to manage other users' collection access.
            // This is not added to collectionService.GetOrganizationCollectionsAsync as that may have
            // unintended consequences on other logic that also uses that method.
            // This is a quick fix but it will be properly fixed by permission changes in Flexible Collections.

            // Get all collections for organization
            orgCollections = await _collectionRepository.GetManyByOrganizationIdAsync(orgId);
        }
        else
        {
            // Returns all collections or collections the user is assigned to, depending on permissions
            orgCollections = await _collectionService.GetOrganizationCollectionsAsync(orgId);
        }

        return orgCollections.Select(c => new CollectionResponseModel(c));
    }

    public async Task<bool> FlexibleCollectionsIsEnabledAsync(Guid organizationId)
    {
        var organizationAbility = await _applicationCacheService.GetOrganizationAbilityAsync(organizationId);
        return organizationAbility?.FlexibleCollections ?? false;
    }

    private async Task<IEnumerable<CollectionResponseModel>> GetByOrgId_vNext(ClaimsPrincipal user, Guid orgId)
    {
        IEnumerable<Collection> orgCollections;

        var readAllAuthorized = (await _authorizationService.AuthorizeAsync(user, CollectionOperations.ReadAll(orgId))).Succeeded;
        if (readAllAuthorized)
        {
            orgCollections = await _collectionRepository.GetManyByOrganizationIdAsync(orgId);
        }
        else
        {
            var assignedCollections = await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value, false);
            orgCollections = assignedCollections.Where(c => c.OrganizationId == orgId && c.Manage).ToList();
        }

        return orgCollections.Select(c => new CollectionResponseModel(c));
    }
}

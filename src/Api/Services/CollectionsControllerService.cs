using System.Security.Claims;
using Bit.Api.Models.Response;
using Bit.Api.Utilities;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
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

    public async Task<IEnumerable<CollectionAccessDetailsResponseModel>> GetManyWithDetails(ClaimsPrincipal user, Guid orgId)
    {
        if (await FlexibleCollectionsIsEnabledAsync(orgId))
        {
            // New flexible collections logic
            return await GetManyWithDetails_vNext(user, orgId);
        }

        // Old pre-flexible collections logic follows
        if (!await ViewAtLeastOneCollectionAsync(orgId) && !await _currentContext.ManageUsers(orgId) && !await _currentContext.ManageGroups(orgId))
        {
            throw new NotFoundException();
        }

        // We always need to know which collections the current user is assigned to
        var assignedOrgCollections =
            await _collectionRepository.GetManyByUserIdWithAccessAsync(_currentContext.UserId.Value, orgId,
                false);

        if (await _currentContext.ViewAllCollections(orgId) || await _currentContext.ManageUsers(orgId))
        {
            // The user can view all collections, but they may not always be assigned to all of them
            var allOrgCollections = await _collectionRepository.GetManyByOrganizationIdWithAccessAsync(orgId);

            return new List<CollectionAccessDetailsResponseModel>(allOrgCollections.Select(c =>
                new CollectionAccessDetailsResponseModel(c.Item1, c.Item2.Groups, c.Item2.Users)
                {
                    // Manually determine which collections they're assigned to
                    Assigned = assignedOrgCollections.Any(ac => ac.Item1.Id == c.Item1.Id)
                })
            );
        }

        return new List<CollectionAccessDetailsResponseModel>(assignedOrgCollections.Select(c =>
            new CollectionAccessDetailsResponseModel(c.Item1, c.Item2.Groups, c.Item2.Users)
            {
                Assigned = true // Mapping from assignedOrgCollections implies they're all assigned
            })
        );
    }

    [Obsolete("Pre-Flexible Collections logic. Will be replaced by CollectionsAuthorizationHandler.")]
    private async Task<bool> ViewAtLeastOneCollectionAsync(Guid orgId)
    {
        return await _currentContext.ViewAllCollections(orgId) || await _currentContext.ViewAssignedCollections(orgId);
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

    private async Task<IEnumerable<CollectionAccessDetailsResponseModel>> GetManyWithDetails_vNext(ClaimsPrincipal user, Guid orgId)
    {
        // We always need to know which collections the current user is assigned to
        var assignedOrgCollections = await _collectionRepository
            .GetManyByUserIdWithAccessAsync(_currentContext.UserId.Value, orgId, true);

        var readAllAuthorized =
            (await _authorizationService.AuthorizeAsync(user, CollectionOperations.ReadAllWithAccess(orgId))).Succeeded;
        if (readAllAuthorized)
        {
            // The user can view all collections, but they may not always be assigned to all of them
            var allOrgCollections = await _collectionRepository.GetManyByOrganizationIdWithAccessAsync(orgId);

            return new List<CollectionAccessDetailsResponseModel>(allOrgCollections.Select(c =>
                new CollectionAccessDetailsResponseModel(c.Item1, c.Item2.Groups, c.Item2.Users)
                {
                    // Manually determine which collections they're assigned to
                    Assigned = assignedOrgCollections.Any(ac => ac.Item1.Id == c.Item1.Id)
                })
            );
        }

        // Filter the assigned collections to only return those where the user has Manage permission
        var manageableOrgCollections = assignedOrgCollections.Where(c => c.Item1.Manage).ToList();

        return new List<CollectionAccessDetailsResponseModel>(manageableOrgCollections.Select(c =>
            new CollectionAccessDetailsResponseModel(c.Item1, c.Item2.Groups, c.Item2.Users)
            {
                Assigned = true // Mapping from manageableOrgCollections implies they're all assigned
            })
        );
    }
}

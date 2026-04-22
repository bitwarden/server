using System.Diagnostics;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

public class CollectionUserAuthorizationHandler
    : AuthorizationHandler<CollectionUserOperationRequirement, CollectionUserAccessResource>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IFeatureService _featureService;
    private Guid _targetOrganizationId;
    private HashSet<Guid>? _managedCollectionsIds;
    private HashSet<Guid>? _orphanedCollectionsIds;

    public CollectionUserAuthorizationHandler(
        ICurrentContext currentContext,
        ICollectionRepository collectionRepository,
        IApplicationCacheService applicationCacheService,
        IFeatureService featureService)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _applicationCacheService = applicationCacheService;
        _featureService = featureService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionUserOperationRequirement requirement, CollectionUserAccessResource resource)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.CollectionUserCollectionGroupAuthorizationHandlers))
        {
            return;
        }

        var resources = resource.Collections;

        if (resources == null || !resources.Any())
        {
            context.Fail();
            return;
        }

        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        _targetOrganizationId = resources.First().OrganizationId;

        if (resources.Any(r => r.OrganizationId != _targetOrganizationId))
        {
            throw new BadRequestException("Requested collections must belong to the same organization.");
        }

        var org = _currentContext.GetOrganization(_targetOrganizationId);

        var authorized = requirement switch
        {
            not null when requirement == CollectionUserOperations.Create =>
                await CanCreateUserAccessAsync(resource, org),
            not null when requirement == CollectionUserOperations.Update =>
                await CanModifyUserAccessAsync(resources, org),
            not null when requirement == CollectionUserOperations.Delete =>
                await CanModifyUserAccessAsync(resources, org),
            null => throw new UnreachableException(),
            _ => false
        };

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CanCreateUserAccessAsync(
        CollectionUserAccessResource resource, CurrentContextOrganization? org)
    {
        var editingSelf = resource.TargetUserId.HasValue &&
                          resource.TargetUserId.Value == _currentContext.UserId!.Value;

        if (editingSelf && !await AllowAdminAccessToAllCollectionItems(org))
        {
            throw new BadRequestException("You cannot add yourself to a collection.");
        }

        return await CanModifyUserAccessAsync(resource.Collections, org);
    }

    private async Task<bool> CanModifyUserAccessAsync(
        ICollection<Collection> resources, CurrentContextOrganization? org)
    {
        if (await AllowAdminAccessToAllCollectionItems(org) && org?.Permissions.ManageUsers == true)
        {
            return true;
        }

        return await CanUpdateCollectionAsync(resources, org);
    }

    private async Task<bool> CanUpdateCollectionAsync(
        ICollection<Collection> resources, CurrentContextOrganization? org)
    {
        if (org is { Permissions.EditAnyCollection: true })
        {
            return true;
        }

        if (await AllowAdminAccessToAllCollectionItems(org) &&
            org is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin })
        {
            return true;
        }

        if (org is not null)
        {
            var canManage = await CanManageCollectionsAsync(resources, org);
            if (canManage)
            {
                return true;
            }
        }

        return await _currentContext.ProviderUserForOrgAsync(_targetOrganizationId);
    }

    private async Task<bool> CanManageCollectionsAsync(ICollection<Collection> targetCollections,
        CurrentContextOrganization? org)
    {
        if (_managedCollectionsIds == null)
        {
            var allUserCollections = await _collectionRepository
                .GetManyByUserIdAsync(_currentContext.UserId!.Value);

            _managedCollectionsIds = allUserCollections
                .Where(c => c.Manage)
                .Select(c => c.Id)
                .ToHashSet();
        }

        if (targetCollections.All(tc => _managedCollectionsIds.Contains(tc.Id)))
        {
            return true;
        }

        if (org is not ({ Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or { Permissions.EditAnyCollection: true }))
        {
            return false;
        }

        if (_orphanedCollectionsIds == null)
        {
            var orgCollections = await _collectionRepository.GetManyByOrganizationIdWithAccessAsync(_targetOrganizationId);

            _orphanedCollectionsIds = orgCollections.Where(c =>
                    !c.Item2.Users.Any(u => u.Manage) && !c.Item2.Groups.Any(g => g.Manage))
                .Select(c => c.Item1.Id)
                .ToHashSet();
        }

        return targetCollections.All(tc => _orphanedCollectionsIds.Contains(tc.Id) || _managedCollectionsIds.Contains(tc.Id));
    }

    private async Task<OrganizationAbility?> GetOrganizationAbilityAsync(CurrentContextOrganization? organization)
    {
        if (organization == null)
        {
            return null;
        }

        return await _applicationCacheService.GetOrganizationAbilityAsync(organization.Id);
    }

    private async Task<bool> AllowAdminAccessToAllCollectionItems(CurrentContextOrganization? org)
    {
        return await GetOrganizationAbilityAsync(org) is { AllowAdminAccessToAllCollectionItems: true };
    }
}

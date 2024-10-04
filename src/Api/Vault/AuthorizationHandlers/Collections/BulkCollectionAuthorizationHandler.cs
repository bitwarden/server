#nullable enable
using System.Diagnostics;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

/// <summary>
/// Handles authorization logic for Collection objects, including access permissions for users and groups.
/// This uses new logic implemented in the Flexible Collections initiative.
/// </summary>
public class BulkCollectionAuthorizationHandler : BulkAuthorizationHandler<BulkCollectionOperationRequirement, Collection>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IFeatureService _featureService;
    private Guid _targetOrganizationId;
    private HashSet<Guid>? _managedCollectionsIds;

    private HashSet<Guid>? _orphanedCollectionsIds;

    public BulkCollectionAuthorizationHandler(
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
        BulkCollectionOperationRequirement requirement, ICollection<Collection>? resources)
    {
        // Establish pattern of authorization handler null checking passed resources
        if (resources == null || !resources.Any())
        {
            context.Fail();
            return;
        }

        // Acting user is not authenticated, fail
        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        _targetOrganizationId = resources.First().OrganizationId;

        // Ensure all target collections belong to the same organization
        if (resources.Any(tc => tc.OrganizationId != _targetOrganizationId))
        {
            throw new BadRequestException("Requested collections must belong to the same organization.");
        }

        var org = _currentContext.GetOrganization(_targetOrganizationId);

        var authorized = false;

        switch (requirement)
        {
            case not null when requirement == BulkCollectionOperations.Create:
                authorized = await CanCreateAsync(org);
                break;

            case not null when requirement == BulkCollectionOperations.Read:
            case not null when requirement == BulkCollectionOperations.ReadAccess:
                authorized = await CanReadAsync(resources, org);
                break;

            case not null when requirement == BulkCollectionOperations.ReadWithAccess:
                authorized = await CanReadWithAccessAsync(resources, org);
                break;

            case not null when requirement == BulkCollectionOperations.Update:
            case not null when requirement == BulkCollectionOperations.ImportCiphers:
                authorized = await CanUpdateCollectionAsync(resources, org);
                break;

            case not null when requirement == BulkCollectionOperations.ModifyUserAccess:
                authorized = await CanUpdateUserAccessAsync(resources, org);
                break;

            case not null when requirement == BulkCollectionOperations.ModifyGroupAccess:
                authorized = await CanUpdateGroupAccessAsync(resources, org);
                break;

            case not null when requirement == BulkCollectionOperations.Delete:
                authorized = await CanDeleteAsync(resources, org);
                break;

            case null:
                // requirement isn't actually nullable but since we use the
                // not null when trick it makes the compiler think that requirement
                // could actually be nullable.
                throw new UnreachableException();
        }

        if (authorized)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CanCreateAsync(CurrentContextOrganization? org)
    {
        // Owners, Admins, and users with CreateNewCollections permission can always create collections
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.CreateNewCollections: true })
        {
            return true;
        }

        var organizationAbility = await GetOrganizationAbilityAsync(org);

        var limitCollectionCreationEnabled = _featureService.IsEnabled(FeatureFlagKeys.LimitCollectionCreationDeletionSplit)
            ? organizationAbility is { LimitCollectionCreation: true }
            : organizationAbility is { LimitCollectionCreationDeletion: true };

        // If the limit collection management setting is disabled, allow any user to create collections
        if (!limitCollectionCreationEnabled)
        {
            return true;
        }

        // Allow provider users to create collections if they are a provider for the target organization
        return await _currentContext.ProviderUserForOrgAsync(_targetOrganizationId);
    }

    private async Task<bool> CanReadAsync(ICollection<Collection> resources, CurrentContextOrganization? org)
    {
        // Owners, Admins, and users with EditAnyCollection or DeleteAnyCollection permission can always read a collection
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.EditAnyCollection: true } or
        { Permissions.DeleteAnyCollection: true })
        {
            return true;
        }

        // The acting user is a member of the target organization,
        // ensure they have access for the collection being read
        if (org is not null)
        {
            var canManageCollections = await CanManageCollectionsAsync(resources, org);
            if (canManageCollections)
            {
                return true;
            }
        }

        // Allow provider users to read collections if they are a provider for the target organization
        return await _currentContext.ProviderUserForOrgAsync(_targetOrganizationId);
    }

    private async Task<bool> CanReadWithAccessAsync(ICollection<Collection> resources, CurrentContextOrganization? org)
    {
        // Owners, Admins, and users with EditAnyCollection, DeleteAnyCollection or ManageUsers permission can always read a collection
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.EditAnyCollection: true } or
        { Permissions.DeleteAnyCollection: true } or
        { Permissions.ManageUsers: true })
        {
            return true;
        }

        // The acting user is a member of the target organization,
        // ensure they have access with manage permission for the collection being read
        if (org is not null)
        {
            var canManageCollections = await CanManageCollectionsAsync(resources, org);
            if (canManageCollections)
            {
                return true;
            }
        }

        // Allow provider users to read collections if they are a provider for the target organization
        return await _currentContext.ProviderUserForOrgAsync(_targetOrganizationId);
    }

    /// <summary>
    /// Ensures the acting user is allowed to update the target collections or manage access permissions for them.
    /// </summary>
    private async Task<bool> CanUpdateCollectionAsync(ICollection<Collection> resources, CurrentContextOrganization? org)
    {
        // Users with EditAnyCollection permission can always update a collection
        if (org is { Permissions.EditAnyCollection: true })
        {
            return true;
        }

        // Owners and Admins can update any collection only if permitted by collection management settings
        if (await AllowAdminAccessToAllCollectionItems(org) && org is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin })
        {
            return true;
        }

        // The acting user is a member of the target organization,
        // ensure they have manage permission for the collection being managed
        if (org is not null)
        {
            var canManageCollections = await CanManageCollectionsAsync(resources, org);
            if (canManageCollections)
            {
                return true;
            }
        }

        // Allow providers to manage collections if they are a provider for the target organization
        return await _currentContext.ProviderUserForOrgAsync(_targetOrganizationId);
    }

    private async Task<bool> CanUpdateUserAccessAsync(ICollection<Collection> resources, CurrentContextOrganization? org)
    {
        if (await AllowAdminAccessToAllCollectionItems(org) && org?.Permissions.ManageUsers == true)
        {
            return true;
        }

        return await CanUpdateCollectionAsync(resources, org);
    }

    private async Task<bool> CanUpdateGroupAccessAsync(ICollection<Collection> resources, CurrentContextOrganization? org)
    {
        if (await AllowAdminAccessToAllCollectionItems(org) && org?.Permissions.ManageGroups == true)
        {
            return true;
        }

        return await CanUpdateCollectionAsync(resources, org);
    }

    private async Task<bool> CanDeleteAsync(ICollection<Collection> resources, CurrentContextOrganization? org)
    {
        // Users with DeleteAnyCollection permission can always delete collections
        if (org is { Permissions.DeleteAnyCollection: true })
        {
            return true;
        }

        // If AllowAdminAccessToAllCollectionItems is true, Owners and Admins can delete any collection, regardless of LimitCollectionCreationDeletion setting
        if (await AllowAdminAccessToAllCollectionItems(org) && org is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin })
        {
            return true;
        }

        // If LimitCollectionCreationDeletion is false, AllowAdminAccessToAllCollectionItems setting is irrelevant.
        // Ensure acting user has manage permissions for all collections being deleted
        // If LimitCollectionCreationDeletion is true, only Owners and Admins can delete collections they manage
        var organizationAbility = await GetOrganizationAbilityAsync(org);

        var limitCollectionDeletionEnabled = _featureService.IsEnabled(FeatureFlagKeys.LimitCollectionCreationDeletionSplit)
            ? organizationAbility is { LimitCollectionDeletion: true }
            : organizationAbility is { LimitCollectionCreationDeletion: true };

        var canDeleteManagedCollections =
            !limitCollectionDeletionEnabled ||
            org is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin };

        if (canDeleteManagedCollections && await CanManageCollectionsAsync(resources, org))
        {
            return true;
        }

        // Allow providers to delete collections if they are a provider for the target organization
        return await _currentContext.ProviderUserForOrgAsync(_targetOrganizationId);
    }

    private async Task<bool> CanManageCollectionsAsync(ICollection<Collection> targetCollections,
        CurrentContextOrganization? org)
    {
        if (_managedCollectionsIds == null)
        {
            var allUserCollections = await _collectionRepository
                .GetManyByUserIdAsync(_currentContext.UserId!.Value);

            var managedCollectionIds = allUserCollections
                .Where(c => c.Manage)
                .Select(c => c.Id);

            _managedCollectionsIds = managedCollectionIds
                .ToHashSet();
        }

        var canManageTargetCollections = targetCollections.All(tc => _managedCollectionsIds.Contains(tc.Id));

        // The user can manage all target collections, stop here, return true.
        if (canManageTargetCollections)
        {
            return true;
        }

        // The user is not assigned to manage all target collections
        // If the user is an Owner/Admin/Custom user with edit, check if any targets are orphaned collections
        if (org is not ({ Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or { Permissions.EditAnyCollection: true }))
        {
            // User is not allowed to manage orphaned collections
            return false;
        }

        if (_orphanedCollectionsIds == null)
        {
            var orgCollections = await _collectionRepository.GetManyByOrganizationIdWithAccessAsync(_targetOrganizationId);

            // Orphaned collections are collections that have no users or groups with manage permissions
            _orphanedCollectionsIds = orgCollections.Where(c =>
                    !c.Item2.Users.Any(u => u.Manage) && !c.Item2.Groups.Any(g => g.Manage))
                .Select(c => c.Item1.Id)
                .ToHashSet();
        }

        return targetCollections.All(tc => _orphanedCollectionsIds.Contains(tc.Id) || _managedCollectionsIds.Contains(tc.Id));
    }

    private async Task<OrganizationAbility?> GetOrganizationAbilityAsync(CurrentContextOrganization? organization)
    {
        // If the CurrentContextOrganization is null, then the user isn't a member of the org so the setting is
        // irrelevant
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

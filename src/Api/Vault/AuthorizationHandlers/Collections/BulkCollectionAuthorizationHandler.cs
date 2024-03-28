#nullable enable
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

        switch (requirement)
        {
            case not null when requirement == BulkCollectionOperations.Create:
                await CanCreateAsync(context, requirement, org);
                break;

            case not null when requirement == BulkCollectionOperations.Read:
            case not null when requirement == BulkCollectionOperations.ReadAccess:
                await CanReadAsync(context, requirement, resources, org);
                break;

            case not null when requirement == BulkCollectionOperations.ReadWithAccess:
                await CanReadWithAccessAsync(context, requirement, resources, org);
                break;

            case not null when requirement == BulkCollectionOperations.Update:
            case not null when requirement == BulkCollectionOperations.ModifyAccess:
            case not null when requirement == BulkCollectionOperations.ImportCiphers:
                await CanUpdateCollectionAsync(context, requirement, resources, org);
                break;

            case not null when requirement == BulkCollectionOperations.Delete:
                await CanDeleteAsync(context, requirement, resources, org);
                break;
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context, IAuthorizationRequirement requirement,
        CurrentContextOrganization? org)
    {
        // Owners, Admins, and users with CreateNewCollections permission can always create collections
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.CreateNewCollections: true })
        {
            context.Succeed(requirement);
            return;
        }

        // If the limit collection management setting is disabled, allow any user to create collections
        if (await GetOrganizationAbilityAsync(org) is { LimitCollectionCreationDeletion: false })
        {
            context.Succeed(requirement);
            return;
        }

        // Allow provider users to create collections if they are a provider for the target organization
        if (await _currentContext.ProviderUserForOrgAsync(_targetOrganizationId))
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanReadAsync(AuthorizationHandlerContext context, IAuthorizationRequirement requirement,
        ICollection<Collection> resources, CurrentContextOrganization? org)
    {
        // Owners, Admins, and users with EditAnyCollection or DeleteAnyCollection permission can always read a collection
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.EditAnyCollection: true } or
        { Permissions.DeleteAnyCollection: true })
        {
            context.Succeed(requirement);
            return;
        }

        // The acting user is a member of the target organization,
        // ensure they have access for the collection being read
        if (org is not null)
        {
            var canManageCollections = await CanManageCollectionsAsync(resources, org);
            if (canManageCollections)
            {
                context.Succeed(requirement);
                return;
            }
        }

        // Allow provider users to read collections if they are a provider for the target organization
        if (await _currentContext.ProviderUserForOrgAsync(_targetOrganizationId))
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanReadWithAccessAsync(AuthorizationHandlerContext context, IAuthorizationRequirement requirement,
        ICollection<Collection> resources, CurrentContextOrganization? org)
    {
        // Owners, Admins, and users with EditAnyCollection, DeleteAnyCollection or ManageUsers permission can always read a collection
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.EditAnyCollection: true } or
        { Permissions.DeleteAnyCollection: true } or
        { Permissions.ManageUsers: true })
        {
            context.Succeed(requirement);
            return;
        }

        // The acting user is a member of the target organization,
        // ensure they have access with manage permission for the collection being read
        if (org is not null)
        {
            var canManageCollections = await CanManageCollectionsAsync(resources, org);
            if (canManageCollections)
            {
                context.Succeed(requirement);
                return;
            }
        }

        // Allow provider users to read collections if they are a provider for the target organization
        if (await _currentContext.ProviderUserForOrgAsync(_targetOrganizationId))
        {
            context.Succeed(requirement);
        }
    }

    /// <summary>
    /// Ensures the acting user is allowed to update the target collections or manage access permissions for them.
    /// </summary>
    private async Task CanUpdateCollectionAsync(AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement, ICollection<Collection> resources,
        CurrentContextOrganization? org)
    {
        // Users with EditAnyCollection permission can always update a collection
        if (org is
            { Permissions.EditAnyCollection: true })
        {
            context.Succeed(requirement);
            return;
        }

        // If V1 is enabled, Owners and Admins can update any collection only if permitted by collection management settings
        var organizationAbility = await GetOrganizationAbilityAsync(org);
        if ((organizationAbility is { AllowAdminAccessToAllCollectionItems: true }  || !_featureService.IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1)) &&
            org is { Type: OrganizationUserType.Owner or OrganizationUserType.Admin })
        {
            context.Succeed(requirement);
            return;
        }

        // The acting user is a member of the target organization,
        // ensure they have manage permission for the collection being managed
        if (org is not null)
        {
            var canManageCollections = await CanManageCollectionsAsync(resources, org);
            if (canManageCollections)
            {
                context.Succeed(requirement);
                return;
            }
        }

        // Allow providers to manage collections if they are a provider for the target organization
        if (await _currentContext.ProviderUserForOrgAsync(_targetOrganizationId))
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanDeleteAsync(AuthorizationHandlerContext context, IAuthorizationRequirement requirement,
        ICollection<Collection> resources, CurrentContextOrganization? org)
    {
        // Owners, Admins, and users with DeleteAnyCollection permission can always delete collections
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.DeleteAnyCollection: true })
        {
            context.Succeed(requirement);
            return;
        }

        // Check for non-null org here: the user must be apart of the organization for this setting to take affect
        // The limit collection management setting is disabled,
        // ensure acting user has manage permissions for all collections being deleted
        if (await GetOrganizationAbilityAsync(org) is { LimitCollectionCreationDeletion: false })
        {
            var canManageCollections = await CanManageCollectionsAsync(resources, org);
            if (canManageCollections)
            {
                context.Succeed(requirement);
                return;
            }
        }

        // Allow providers to delete collections if they are a provider for the target organization
        if (await _currentContext.ProviderUserForOrgAsync(_targetOrganizationId))
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CanManageCollectionsAsync(
        ICollection<Collection> targetCollections,
        CurrentContextOrganization org)
    {
        // List of collection Ids the acting user has access to
        var assignedCollectionIds =
            (await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId!.Value, useFlexibleCollections: true))
            .Where(c =>
                // Check Collections with Manage permission
                c.Manage && c.OrganizationId == org.Id)
            .Select(c => c.Id)
            .ToHashSet();

        // Check if the acting user has access to all target collections
        return targetCollections.All(tc => assignedCollectionIds.Contains(tc.Id));
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
}

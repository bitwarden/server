#nullable enable
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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
    private readonly IFeatureService _featureService;
    private Guid _targetOrganizationId;

    private bool UseFlexibleCollections => _featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext);

    public BulkCollectionAuthorizationHandler(
        ICurrentContext currentContext,
        ICollectionRepository collectionRepository,
        IFeatureService featureService)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _featureService = featureService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        BulkCollectionOperationRequirement requirement, ICollection<Collection>? resources)
    {
        if (!UseFlexibleCollections)
        {
            // Flexible collections is OFF, should not be using this handler
            throw new FeatureUnavailableException("Flexible collections is OFF when it should be ON.");
        }

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
                await CanReadAsync(context, requirement, resources, org);
                break;

            case not null when requirement == BulkCollectionOperations.ReadAccess:
                await CanReadAccessAsync(context, requirement, resources, org);
                break;

            case not null when requirement == BulkCollectionOperations.Update:
            case not null when requirement == BulkCollectionOperations.ModifyAccess:
                await CanManageCollectionAccessAsync(context, requirement, resources, org);
                break;

            case not null when requirement == BulkCollectionOperations.Delete:
                await CanDeleteAsync(context, requirement, resources, org);
                break;
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context, IAuthorizationRequirement requirement,
        CurrentContextOrganization? org)
    {
        // If the limit collection management setting is disabled, allow any user to create collections
        // Otherwise, Owners, Admins, and users with CreateNewCollections permission can always create collections
        if (org is
        { LimitCollectionCreationDeletion: false } or
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.CreateNewCollections: true })
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
        { LimitCollectionCreationDeletion: false } or
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.EditAnyCollection: true } or
        { Permissions.DeleteAnyCollection: true } or
        { Permissions.CreateNewCollections: true } or
        { Permissions.ManageUsers: true })
        {
            context.Succeed(requirement);
            return;
        }

        // The acting user is a member of the target organization,
        // ensure they have access for the collection being read
        if (org is not null)
        {
            var canManageCollections = await HasCollectionAccessAsync(resources, org, requireManagePermission: false);
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

    private async Task CanReadAccessAsync(AuthorizationHandlerContext context, IAuthorizationRequirement requirement,
        ICollection<Collection> resources, CurrentContextOrganization? org)
    {
        // Owners, Admins, and users with EditAnyCollection or DeleteAnyCollection permission can always read a collection
        if (org is
        { LimitCollectionCreationDeletion: false } or
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.EditAnyCollection: true } or
        { Permissions.DeleteAnyCollection: true } or
        { Permissions.CreateNewCollections: true })
        {
            context.Succeed(requirement);
            return;
        }

        // The acting user is a member of the target organization,
        // ensure they have access for the collection being read
        if (org is not null)
        {
            var canManageCollections = await HasCollectionAccessAsync(resources, org, requireManagePermission: false);
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
    /// Ensures the acting user is allowed to manage access permissions for the target collections.
    /// </summary>
    private async Task CanManageCollectionAccessAsync(AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement, ICollection<Collection> resources,
        CurrentContextOrganization? org)
    {
        // Owners, Admins, and users with EditAnyCollection permission can always manage collection access
        if (org is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin } or
        { Permissions.EditAnyCollection: true })
        {
            context.Succeed(requirement);
            return;
        }

        // The acting user is a member of the target organization,
        // ensure they have manage permission for the collection being managed
        if (org is not null)
        {
            var canManageCollections = await HasCollectionAccessAsync(resources, org, requireManagePermission: true);
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

        // The limit collection management setting is disabled,
        // ensure acting user has manage permissions for all collections being deleted
        if (org is { LimitCollectionCreationDeletion: false })
        {
            var canManageCollections = await HasCollectionAccessAsync(resources, org, requireManagePermission: true);
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

    private async Task<bool> HasCollectionAccessAsync(
        ICollection<Collection> targetCollections,
        CurrentContextOrganization org,
        bool requireManagePermission)
    {
        // List of collection Ids the acting user has access to
        var manageableCollectionIds =
            (await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId!.Value))
            .Where(c =>
                // If requireManagePermission is true, check Collections with Manage permission
                (!requireManagePermission || c.Manage)
                && c.OrganizationId == org.Id)
            .Select(c => c.Id)
            .ToHashSet();

        // Check if the acting user has access to all target collections
        return targetCollections.All(tc => manageableCollectionIds.Contains(tc.Id));
    }
}

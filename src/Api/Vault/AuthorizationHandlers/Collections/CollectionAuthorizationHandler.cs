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
public class CollectionAuthorizationHandler : BulkAuthorizationHandler<CollectionOperationRequirement, Collection>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IFeatureService _featureService;

    private bool FlexibleCollectionsIsEnabled => _featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext);

    public CollectionAuthorizationHandler(ICurrentContext currentContext, ICollectionRepository collectionRepository,
        IFeatureService featureService)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _featureService = featureService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionOperationRequirement requirement, ICollection<Collection> resources)
    {
        if (!FlexibleCollectionsIsEnabled)
        {
            // Flexible collections is OFF, should not be using this handler
            throw new FeatureUnavailableException("Flexible collections is OFF when it should be ON.");
        }

        // Establish pattern of authorization handler null checking passed resources
        if (resources == null)
        {
            context.Fail();
            return;
        }

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

        switch (requirement)
        {
            case not null when requirement == CollectionOperations.Create:
                await CanCreateAsync(context, requirement, org);
                break;

            case not null when requirement.Name == nameof(CollectionOperations.ReadAll):
                await CanReadAllAsync(context, requirement, org);
                break;

            case not null when requirement == CollectionOperations.Delete:
                await CanDeleteAsync(context, requirement, resources, org);
                break;

            case not null when requirement == CollectionOperations.ModifyAccess:
                await CanManageCollectionAccessAsync(context, requirement, resources, org);
                break;

            default:
                context.Fail();
                break;
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        CurrentContextOrganization org)
    {
        // If false, all organization members are allowed to create collections
        if (!org.LimitCollectionCreationDeletion)
        {
            context.Succeed(requirement);
            return;
        }

        // Owners, Admins, Providers, and users with CreateNewCollections permission can always create collections
        if (
            org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin ||
            org.Permissions is { CreateNewCollections: true } ||
            await _currentContext.ProviderUserForOrgAsync(org.Id))
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail();
    }

    private async Task CanReadAllAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        CurrentContextOrganization org)
    {
        if (org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin ||
            org.Permissions.ManageGroups ||
            org.Permissions.ManageUsers ||
            org.Permissions.EditAnyCollection ||
            org.Permissions.DeleteAnyCollection ||
            org.Permissions.AccessImportExport)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail();
    }

    private async Task CanDeleteAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        ICollection<Collection> resources, CurrentContextOrganization org)
    {
        // Owners, Admins, Providers, and users with DeleteAnyCollection permission can always delete collections
        if (
            org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin ||
            org.Permissions is { DeleteAnyCollection: true } ||
            await _currentContext.ProviderUserForOrgAsync(org.Id))
        {
            context.Succeed(requirement);
            return;
        }

        // The limit collection management setting is enabled and we are not an Admin (above condition), fail
        if (org.LimitCollectionCreationDeletion)
        {
            context.Fail();
            return;
        }

        // Other members types should have the Manage capability for all collections being deleted
        var manageableCollectionIds =
            (await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId!.Value))
            .Where(c => c.Manage && c.OrganizationId == org.Id)
            .Select(c => c.Id)
            .ToHashSet();

        // The acting user does not have permission to manage all target collections, fail
        if (resources.Any(c => !manageableCollectionIds.Contains(c.Id)))
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }

    /// <summary>
    /// Ensures the acting user is allowed to manage access permissions for the target collections.
    /// </summary>
    private async Task CanManageCollectionAccessAsync(AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement, ICollection<Collection> targetCollections, CurrentContextOrganization org)
    {
        // Owners, Admins, Providers, and users with EditAnyCollection permission can always manage collection access
        if (
            org.Permissions is { EditAnyCollection: true } ||
            org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin ||
            await _currentContext.ProviderUserForOrgAsync(org.Id))
        {
            context.Succeed(requirement);
            return;
        }

        // List of collection Ids the acting user is allowed to manage
        var manageableCollectionIds =
            (await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId!.Value))
            .Where(c => c.Manage && c.OrganizationId == org.Id)
            .Select(c => c.Id)
            .ToHashSet();

        // The acting user does not have permission to manage all target collections, fail
        if (targetCollections.Any(tc => !manageableCollectionIds.Contains(tc.Id)))
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }
}

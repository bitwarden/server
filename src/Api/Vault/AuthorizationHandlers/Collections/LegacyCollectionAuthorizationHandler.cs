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
/// This uses old pre-Flexible Collections logic and will be removed when that initiative is fully released.
/// </summary>
public class LegacyCollectionAuthorizationHandler : BulkAuthorizationHandler<CollectionOperationRequirement, Collection>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IFeatureService _featureService;
    private readonly ICollectionService _collectionService;

    public LegacyCollectionAuthorizationHandler(ICurrentContext currentContext, ICollectionRepository collectionRepository,
        IFeatureService featureService, ICollectionService collectionService)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _featureService = featureService;
        _collectionService = collectionService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionOperationRequirement requirement, ICollection<Collection> resources)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.FlexibleCollections, _currentContext))
        {
            // Flexible collections is ON, do not use the legacy logic in this handler
            return;
        }

        // Establish pattern of authorization handler null checking passed resources
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

        var targetOrganizationId = resources.First().OrganizationId;

        // Ensure all target collections belong to the same organization
        if (resources.Any(tc => tc.OrganizationId != targetOrganizationId))
        {
            throw new BadRequestException("Requested collections must belong to the same organization.");
        }

        // TODO: this will not work for providers (new or legacy implementations)
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

            case not null when requirement == CollectionOperations.Delete:
                await CanDeleteAsync(context, requirement, resources, org);
                break;

            case not null when requirement == CollectionOperations.ModifyAccess:
                await CanManageCollectionAccessAsync(context, requirement, resources, org);
                break;
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        CurrentContextOrganization org)
    {
        if (await _currentContext.OrganizationManager(org.Id) || (_currentContext.Organizations?.Any(o => o.Id == org.Id
                && (o.Permissions?.CreateNewCollections ?? false)) ?? false))
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanDeleteAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        ICollection<Collection> resources, CurrentContextOrganization org)
    {
        // Delete any collection - moved from CurrentContext as this was its only use
        var deleteAnyCollection = await _currentContext.OrganizationAdmin(org.Id) ||
            (_currentContext.Organizations?.Any(o =>
                o.Id == org.Id &&
                (o.Permissions?.DeleteAnyCollection ?? false)) ?? false);
        if (deleteAnyCollection)
        {
            context.Succeed(requirement);
            return;
        }

        // Delete assigned collections
        var collectionIds = resources.Select(c => c.Id);
        if (!await _currentContext.DeleteAssignedCollections(org.Id))
        {
            context.Fail();
            return;
        }

        var userCollections = await _collectionService.GetOrganizationCollectionsAsync(org.Id);
        var filteredCollections = userCollections.Where(c => collectionIds.Contains(c.Id) && c.OrganizationId == org.Id);

        if (filteredCollections.Count() == resources.Count)
        {
            // User is assigned to all collections we're operating on
            context.Succeed(requirement);
        }
    }

    /// <summary>
    /// Ensures the acting user is allowed to manage access permissions for the target collections.
    /// </summary>
    private async Task CanManageCollectionAccessAsync(AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement, ICollection<Collection> targetCollections, CurrentContextOrganization org)
    {

        // TODO: implement old logic
        // TODO: remove CanEditCollectionAsync from controller

        // new logic follows

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

using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

public class CollectionAuthorizationHandler : BulkAuthorizationHandler<CollectionOperationRequirement, Collection>
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
        // If false, all organization members are allowed to create collections
        if (!org.LimitCollectionCdOwnerAdmin)
        {
            context.Succeed(requirement);
            return;
        }

        // Owners, Admins, Providers, and users with CreateNewCollections or EditAnyCollection permission can always create collections
        if (
            org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin ||
            org.Permissions.CreateNewCollections || org.Permissions.EditAnyCollection ||
            await _currentContext.ProviderUserForOrgAsync(org.Id))
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail();
    }

    private async Task CanDeleteAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        ICollection<Collection> resources, CurrentContextOrganization org)
    {
        // Owners, Admins, Providers, and users with DeleteAnyCollection or EditAnyCollection permission can always delete collections
        if (
            org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin ||
            org.Permissions.DeleteAnyCollection || org.Permissions.EditAnyCollection ||
            await _currentContext.ProviderUserForOrgAsync(org.Id))
        {
            context.Succeed(requirement);
            return;
        }

        // The limit collection management setting is enabled and we are not an Admin (above condition), fail
        if (org.LimitCollectionCdOwnerAdmin)
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

using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.AuthorizationHandlers;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

public class CollectionAuthorizationHandler : BulkAuthorizationHandler<CollectionOperationRequirement, Collection>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public CollectionAuthorizationHandler(ICurrentContext currentContext, ICollectionRepository collectionRepository, IOrganizationUserRepository organizationUserRepository)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _organizationUserRepository = organizationUserRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CollectionOperationRequirement requirement, ICollection<Collection> resources)
    {
        switch (requirement)
        {
            case not null when requirement == CollectionOperations.Create:
                await CanCreateAsync(context, requirement, resources);
                break;

            case not null when requirement == CollectionOperations.Delete:
                await CanDeleteAsync(context, requirement, resources);
                break;

            case not null when requirement == CollectionOperations.ModifyAccess:
                await CanManageCollectionAccessAsync(context, requirement, resources);
                break;
        }
    }

    private async Task CanCreateAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement, ICollection<Collection> resources)
    {
        // Bulk creation of collections is not supported
        if (resources.Count > 1)
        {
            throw new NotSupportedException();
        }

        // Acting user is not a member of the target organization, fail
        var org = _currentContext.GetOrganization(resources.First().OrganizationId);
        if (org == null)
        {
            context.Fail();
            return;
        }

        var success = !org.LimitCollectionCdOwnerAdmin || (org.Type == OrganizationUserType.Owner ||
                                                           org.Type == OrganizationUserType.Admin ||
                                                           org.Permissions.CreateNewCollections ||
                                                           org.Permissions.EditAnyCollection ||
                                                           await _currentContext.ProviderUserForOrgAsync(org.Id));
        if (success)
        {
            context.Succeed(requirement);
        }
    }

    private async Task CanDeleteAsync(AuthorizationHandlerContext context, CollectionOperationRequirement requirement,
        ICollection<Collection> resources)
    {
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

        // Owners, Admins, Providers, and users with DeleteAnyCollection or EditAnyCollection permission can always delete collections
        if (
            org.Permissions is { DeleteAnyCollection: true, EditAnyCollection: true } ||
            org.Type is OrganizationUserType.Admin or OrganizationUserType.Owner ||
            await _currentContext.ProviderUserForOrgAsync(org.Id))
        {
            context.Succeed(requirement);
            return;
        }

        // Other members types should have the Manage capability for all collections being deleted
        if (!org.LimitCollectionCdOwnerAdmin)
        {
            var manageableCollectionIds =
                (await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value))
                .Where(c => c.Manage && c.OrganizationId == org.Id)
                .Select(c => c.Id)
                .ToHashSet();

            // The acting user does not have permission to manage all target collections, fail
            if (resources.Any(c => !manageableCollectionIds.Contains(c.Id)))
            {
                context.Fail();
                return;
            }
        }
        else
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }

    /// <summary>
    /// Ensures the acting user is allowed to manage access permissions for the target collections.
    /// </summary>
    private async Task CanManageCollectionAccessAsync(AuthorizationHandlerContext context, IAuthorizationRequirement requirement, ICollection<Collection> targetCollections)
    {
        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        var targetOrganizationId = targetCollections.First().OrganizationId;

        // Ensure all target collections belong to the same organization
        if (targetCollections.Any(tc => tc.OrganizationId != targetOrganizationId))
        {
            throw new BadRequestException("Requested collections must belong to the same organization.");
        }

        var org = (await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, _currentContext.UserId.Value))
            .FirstOrDefault(o => targetOrganizationId == o.Id);

        // Acting user is not a member of the target organization, fail
        if (org == null)
        {
            context.Fail();
            return;
        }

        // Owners, Admins, Providers, and users with EditAnyCollection permission can always manage collection access
        if (
            org.Permissions is { EditAnyCollection: true } ||
            org.Type is OrganizationUserType.Admin or OrganizationUserType.Owner ||
            await _currentContext.ProviderUserForOrgAsync(org.Id))
        {
            context.Succeed(requirement);
            return;
        }

        // List of collection Ids the acting user is allowed to manage
        var manageableCollectionIds =
            (await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value))
            .Where(c => c.Manage && c.OrganizationId == targetOrganizationId)
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

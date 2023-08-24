using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.Vault.AuthorizationHandlers;

public class CollectionAccessAuthorizationHandler : BulkAuthorizationHandler<CollectionAccessOperationRequirement, ICollectionAccess>
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public CollectionAccessAuthorizationHandler(ICurrentContext currentContext, ICollectionRepository collectionRepository, IOrganizationUserRepository organizationUserRepository)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _organizationUserRepository = organizationUserRepository;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CollectionAccessOperationRequirement requirement,
        ICollection<ICollectionAccess> resources)
    {
        switch (requirement)
        {
            case not null when requirement == CollectionAccessOperation.CreateUpdateDelete:
                await CanManageCollectionAccessAsync(context, requirement, resources.Select(c => c.CollectionId));
                break;
        }
    }

    /// <summary>
    /// Ensures the acting user is allowed to manage access permissions for the target collections.
    /// </summary>
    private async Task CanManageCollectionAccessAsync(AuthorizationHandlerContext context, IAuthorizationRequirement requirement, IEnumerable<Guid> collectionIds)
    {
        if (!_currentContext.UserId.HasValue)
        {
            context.Fail();
            return;
        }

        var distinctTargetCollectionIds = collectionIds.Distinct().ToList();

        // List of collections the user is performing the operation on
        var targetCollections = await _collectionRepository.GetManyByManyIdsAsync(distinctTargetCollectionIds);

        // A target collection does not exist, fail the requirement
        if (targetCollections.Count != distinctTargetCollectionIds.Count)
        {
            context.Fail();
            return;
        }

        var targetOrganizationId = targetCollections.First().OrganizationId;

        // Ensure all target collections belong to the same organization
        if (targetCollections.Any(tc => tc.OrganizationId != targetOrganizationId))
        {
            context.Fail();
            return;
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
            .Where(c => c.Manage)
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

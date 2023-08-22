using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public abstract class CollectionAccessAuthorizationHandlerBase<TRequirement, TResource> : BulkAuthorizationHandler<TRequirement, TResource>
    where TRequirement : IAuthorizationRequirement
{
    private readonly ICurrentContext _currentContext;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    protected CollectionAccessAuthorizationHandlerBase(ICurrentContext currentContext, ICollectionRepository collectionRepository, IOrganizationUserRepository organizationUserRepository)
    {
        _currentContext = currentContext;
        _collectionRepository = collectionRepository;
        _organizationUserRepository = organizationUserRepository;
    }

    /// <summary>
    /// Ensures the acting user is allowed to manage access permissions for the target collections.
    /// </summary>
    protected async Task CanManageCollectionAccessAsync(AuthorizationHandlerContext context, IAuthorizationRequirement requirement, IEnumerable<Guid> collectionIds)
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

        var userOrgs = await _currentContext.OrganizationMembershipAsync(_organizationUserRepository, _currentContext.UserId.Value);
        var distinctTargetOrganizationIds = targetCollections.Select(tc => tc.OrganizationId).Distinct();
        var restrictedOrganizations = new List<CurrentContentOrganization>();

        foreach (var orgId in distinctTargetOrganizationIds)
        {
            var org = userOrgs.FirstOrDefault(o => orgId == o.Id);

            // Acting user is not a member of the target organization, fail
            if (org == null)
            {
                context.Fail();
                return;
            }

            // Owner/Admins or users with EditAnyCollection permission can always manage collection access
            if (
                org.Permissions.EditAnyCollection ||
                org.Type is OrganizationUserType.Admin or OrganizationUserType.Owner ||
                await _currentContext.ProviderUserForOrgAsync(org.Id))
            {
                continue;
            }

            restrictedOrganizations.Add(org);
        }

        // All target collections belong to organizations the acting user is allowed to manage collection access, succeed
        if (restrictedOrganizations.Count == 0)
        {
            context.Succeed(requirement);
            return;
        }

        // List of collections the acting user is allowed to manage
        var manageableCollections =
            (await _collectionRepository.GetManyByUserIdAsync(_currentContext.UserId.Value))
            .Where(c => c.Manage).ToList();

        foreach (var org in restrictedOrganizations)
        {
            // User must have explicit "Manage" permission on the target collections for this organization
            foreach (var targetCollection in targetCollections.Where(tc => tc.OrganizationId == org.Id))
            {
                // Target collection is not in the list of manageable collections, fail
                if (!manageableCollections.Exists(c => c.Id == targetCollection.Id))
                {
                    context.Fail();
                    return;
                }
            }
        }

        context.Succeed(requirement);
    }
}

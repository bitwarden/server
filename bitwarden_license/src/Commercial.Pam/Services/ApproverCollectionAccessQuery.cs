using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Commercial.Pam.Services;

public class ApproverCollectionAccessQuery : IApproverCollectionAccessQuery
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;

    public ApproverCollectionAccessQuery(
        ICollectionRepository collectionRepository,
        ICurrentContext currentContext,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationAbilityCacheService organizationAbilityCacheService)
    {
        _collectionRepository = collectionRepository;
        _currentContext = currentContext;
        _organizationUserRepository = organizationUserRepository;
        _organizationAbilityCacheService = organizationAbilityCacheService;
    }

    public async Task<HashSet<Guid>> GetManageableCollectionIdsAsync(Guid userId)
    {
        // Collections the user is assigned with Manage (the repository aggregates Manage across direct and group
        // access), mirroring BulkCollectionAuthorizationHandler. This read already spans disabled organizations.
        var assigned = await _collectionRepository.GetManyByUserIdAsync(userId);
        var manageable = assigned.Where(c => c.Manage).Select(c => c.Id).ToHashSet();

        // Owners/Admins (when the org permits) and EditAnyCollection custom users can manage every collection in the
        // organization, so fold those collections in too -- from the request context for the user's active orgs.
        var contextOrgIds = new HashSet<Guid>();
        foreach (var org in _currentContext.Organizations)
        {
            contextOrgIds.Add(org.Id);
            await FoldInManageAllCollectionsAsync(org, manageable);
        }

        // A suspended (disabled) organization is absent from the claim-based request context, which would otherwise
        // blank its governance view. Governance stays visible and actionable while an org is suspended, so fold in the
        // user's confirmed memberships the context dropped -- read from the database, which includes disabled orgs.
        var memberships = await _organizationUserRepository.GetManyDetailsByUserAsync(
            userId, OrganizationUserStatusType.Confirmed);
        foreach (var membership in memberships.Where(ou => !contextOrgIds.Contains(ou.OrganizationId)))
        {
            await FoldInManageAllCollectionsAsync(new CurrentContextOrganization(membership), manageable);
        }

        return manageable;
    }

    public async Task<bool> CanManageCollectionAsync(Guid userId, Guid collectionId)
    {
        var manageable = await GetManageableCollectionIdsAsync(userId);
        return manageable.Contains(collectionId);
    }

    // Folds every collection in the organization into the manageable set when the user can manage all of them.
    private async Task FoldInManageAllCollectionsAsync(CurrentContextOrganization org, HashSet<Guid> manageable)
    {
        var canManageAll = org.Permissions.EditAnyCollection;
        if (!canManageAll && org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin)
        {
            var ability = await _organizationAbilityCacheService.GetOrganizationAbilityAsync(org.Id);
            canManageAll = ability?.AllowAdminAccessToAllCollectionItems ?? false;
        }

        if (!canManageAll)
        {
            return;
        }

        var orgCollections = await _collectionRepository.GetManyByOrganizationIdAsync(org.Id);
        foreach (var collection in orgCollections)
        {
            manageable.Add(collection.Id);
        }
    }
}

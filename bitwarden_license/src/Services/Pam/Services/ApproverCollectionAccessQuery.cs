using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Services.Pam.Services;

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
        // Collections assigned with Manage, directly or via a group, including in disabled organizations.
        var assigned = await _collectionRepository.GetManyByUserIdAsync(userId);
        var manageable = assigned.Where(c => c.Manage).Select(c => c.Id).ToHashSet();

        // Owners/Admins and EditAnyCollection custom users can manage every collection in the organization; fold
        // those in from the request context for the user's active orgs.
        var contextOrgIds = new HashSet<Guid>();
        foreach (var org in _currentContext.Organizations)
        {
            contextOrgIds.Add(org.Id);
            await FoldInManageAllCollectionsAsync(org, manageable);
        }

        // A suspended organization is missing from the claims, so fold in confirmed memberships from the database.
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
        var assigned = await _collectionRepository.GetManyByUserIdAsync(userId);
        if (assigned.Any(c => c.Id == collectionId && c.Manage))
        {
            return true;
        }

        var collection = await _collectionRepository.GetByIdAsync(collectionId);
        if (collection is null)
        {
            return false;
        }

        // A suspended organization is missing from the claims, so fall back to the confirmed membership.
        var org = _currentContext.GetOrganization(collection.OrganizationId);
        if (org is null)
        {
            var membership = await _organizationUserRepository.GetDetailsByUserAsync(
                userId, collection.OrganizationId, OrganizationUserStatusType.Confirmed);
            if (membership is null)
            {
                return false;
            }

            org = new CurrentContextOrganization(membership);
        }

        return await CanManageAllCollectionsAsync(org);
    }

    private async Task FoldInManageAllCollectionsAsync(CurrentContextOrganization org, HashSet<Guid> manageable)
    {
        if (!await CanManageAllCollectionsAsync(org))
        {
            return;
        }

        var orgCollections = await _collectionRepository.GetManyByOrganizationIdAsync(org.Id);
        foreach (var collection in orgCollections)
        {
            manageable.Add(collection.Id);
        }
    }

    private async Task<bool> CanManageAllCollectionsAsync(CurrentContextOrganization org)
    {
        if (org.Permissions.EditAnyCollection)
        {
            return true;
        }

        if (org.Type is not (OrganizationUserType.Owner or OrganizationUserType.Admin))
        {
            return false;
        }

        var ability = await _organizationAbilityCacheService.GetOrganizationAbilityAsync(org.Id);
        return ability?.AllowAdminAccessToAllCollectionItems ?? false;
    }
}

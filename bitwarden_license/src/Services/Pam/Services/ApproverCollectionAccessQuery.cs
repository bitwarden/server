using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Services.Pam.Services;

public class ApproverCollectionAccessQuery : IApproverCollectionAccessQuery
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;

    public ApproverCollectionAccessQuery(
        ICollectionRepository collectionRepository,
        ICurrentContext currentContext,
        IOrganizationAbilityCacheService organizationAbilityCacheService)
    {
        _collectionRepository = collectionRepository;
        _currentContext = currentContext;
        _organizationAbilityCacheService = organizationAbilityCacheService;
    }

    public async Task<HashSet<Guid>> GetManageableCollectionIdsAsync(Guid userId)
    {
        // Collections assigned with Manage, directly or via a group. Suspended organizations are excluded.
        var assigned = await _collectionRepository.GetManyByUserIdAsync(userId);
        var manageable = assigned.Where(c => c.Manage).Select(c => c.Id).ToHashSet();

        // Owners/Admins and EditAnyCollection custom users can manage every collection in an enabled organization.
        foreach (var org in _currentContext.Organizations)
        {
            await FoldInManageAllCollectionsAsync(org, manageable);
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

        var org = _currentContext.GetOrganization(collection.OrganizationId);
        return org is not null && await CanManageAllCollectionsAsync(org);
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
        var isAdmin = org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin;
        if (!org.Permissions.EditAnyCollection && !isAdmin)
        {
            return false;
        }

        var ability = await _organizationAbilityCacheService.GetOrganizationAbilityAsync(org.Id);
        if (ability is not { Enabled: true })
        {
            return false;
        }

        return org.Permissions.EditAnyCollection || ability.AllowAdminAccessToAllCollectionItems;
    }
}

using Bit.Core.Context;
using Bit.Pam.Services;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Commercial.Pam.Services;

public class ApproverCollectionAccessQuery : IApproverCollectionAccessQuery
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IApplicationCacheService _applicationCacheService;

    public ApproverCollectionAccessQuery(
        ICollectionRepository collectionRepository,
        ICurrentContext currentContext,
        IApplicationCacheService applicationCacheService)
    {
        _collectionRepository = collectionRepository;
        _currentContext = currentContext;
        _applicationCacheService = applicationCacheService;
    }

    public async Task<HashSet<Guid>> GetManageableCollectionIdsAsync(Guid userId)
    {
        // Collections the user is assigned with Manage (the repository aggregates Manage across direct and group
        // access), mirroring BulkCollectionAuthorizationHandler.
        var assigned = await _collectionRepository.GetManyByUserIdAsync(userId);
        var manageable = assigned.Where(c => c.Manage).Select(c => c.Id).ToHashSet();

        // Owners/Admins (when the org permits) and EditAnyCollection custom users can manage every collection in the
        // organization, so fold those collections in too.
        foreach (var org in _currentContext.Organizations)
        {
            var canManageAll = org.Permissions.EditAnyCollection;
            if (!canManageAll && org.Type is OrganizationUserType.Owner or OrganizationUserType.Admin)
            {
                var ability = await _applicationCacheService.GetOrganizationAbilityAsync(org.Id);
                canManageAll = ability?.AllowAdminAccessToAllCollectionItems ?? false;
            }

            if (!canManageAll)
            {
                continue;
            }

            var orgCollections = await _collectionRepository.GetManyByOrganizationIdAsync(org.Id);
            foreach (var collection in orgCollections)
            {
                manageable.Add(collection.Id);
            }
        }

        return manageable;
    }

    public async Task<bool> CanManageCollectionAsync(Guid userId, Guid collectionId)
    {
        var manageable = await GetManageableCollectionIdsAsync(userId);
        return manageable.Contains(collectionId);
    }
}

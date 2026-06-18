using Bit.Core.Repositories;
using Bit.Pam.Repositories;

namespace Bit.Pam.Services;

public class SingleActiveLeaseEvaluator : ISingleActiveLeaseEvaluator
{
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAccessRuleRepository _accessRuleRepository;

    public SingleActiveLeaseEvaluator(
        ICollectionCipherRepository collectionCipherRepository,
        ICollectionRepository collectionRepository,
        IAccessRuleRepository accessRuleRepository)
    {
        _collectionCipherRepository = collectionCipherRepository;
        _collectionRepository = collectionRepository;
        _accessRuleRepository = accessRuleRepository;
    }

    public async Task<bool> AppliesAsync(Guid userId, Guid cipherId)
    {
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdCipherIdAsync(userId, cipherId);
        if (collectionCiphers.Count == 0)
        {
            // No reachable collection: there is no path, so the constraint does not bind.
            return false;
        }

        var collectionIds = collectionCiphers.Select(cc => cc.CollectionId).ToHashSet();
        var collections = await _collectionRepository.GetManyByManyIdsAsync(collectionIds);

        var paths = collections.Where(c => collectionIds.Contains(c.Id)).ToList();
        if (paths.Count == 0)
        {
            return false;
        }

        foreach (var collection in paths)
        {
            // An ungated path is an escape: the caller can reach the cipher without any singleton rule, so the
            // constraint does not bind for them.
            if (!collection.AccessRuleId.HasValue)
            {
                return false;
            }

            var accessRule = await _accessRuleRepository.GetByIdAsync(collection.AccessRuleId.Value);

            // A missing rule, or a rule that does not ask for a singleton, is likewise an escape path.
            if (accessRule is null || !accessRule.SingleActiveLease)
            {
                return false;
            }
        }

        // Every path is governed by a singleton rule: the constraint binds.
        return true;
    }
}

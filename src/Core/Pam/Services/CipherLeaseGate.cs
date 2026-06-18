using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Pam.Engine;
using Bit.Core.Pam.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Pam.Services;

/// <inheritdoc cref="ICipherLeaseGate" />
public class CipherLeaseGate : ICipherLeaseGate
{
    private readonly IFeatureService _featureService;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly ICurrentContext _currentContext;
    private readonly TimeProvider _timeProvider;

    public CipherLeaseGate(
        IFeatureService featureService,
        IGoverningRuleResolver resolver,
        IAccessLeaseRepository accessLeaseRepository,
        ICollectionRepository collectionRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICurrentContext currentContext,
        TimeProvider timeProvider)
    {
        _featureService = featureService;
        _resolver = resolver;
        _accessLeaseRepository = accessLeaseRepository;
        _collectionRepository = collectionRepository;
        _collectionCipherRepository = collectionCipherRepository;
        _currentContext = currentContext;
        _timeProvider = timeProvider;
    }

    private bool Enabled => _featureService.IsEnabled(FeatureFlagKeys.Pam);

    public async Task<FullCipherAccess?> AuthorizeReadAsync(Guid userId, Cipher cipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        return await IsBlockedAsync(userId, cipher.Id)
            ? null
            : FullCipherAccess.ForCipher(cipher.Id);
    }

    public Task<FullCipherAccess> AuthorizeReadManyAsync(
        Guid userId,
        IEnumerable<Cipher> ciphers,
        IEnumerable<CollectionDetails>? collections,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher)
    {
        if (!Enabled)
        {
            return Task.FromResult(FullCipherAccess.Unrestricted());
        }

        return Task.FromResult(BuildBulkWitness(ciphers, collections, collectionCiphersByCipher));
    }

    public async Task<FullCipherAccess> AuthorizeReadManyAsync(Guid userId, IEnumerable<Cipher> ciphers)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        var collections = await _collectionRepository.GetManyByUserIdAsync(userId);
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(userId);
        var collectionCiphersByCipher = collectionCiphers.GroupBy(c => c.CipherId).ToDictionary(g => g.Key);
        return BuildBulkWitness(ciphers, collections, collectionCiphersByCipher);
    }

    // Bulk reads never release a gated cipher's secrets, regardless of lease state, so the witness
    // authorizes only the non-gated subset and the gated ones fall through to the partial shape.
    private FullCipherAccess BuildBulkWitness(
        IEnumerable<Cipher> ciphers,
        IEnumerable<CollectionDetails>? collections,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher)
    {
        var gated = GetGatedCipherIds(collections, collectionCiphersByCipher);
        var authorized = ciphers.Select(c => c.Id).Where(id => !gated.Contains(id));
        return FullCipherAccess.ForCiphers(authorized);
    }

    public ISet<Guid> GetGatedCipherIds(
        IEnumerable<CollectionDetails>? collections,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher)
    {
        var gated = new HashSet<Guid>();
        if (!Enabled || collections == null || collectionCiphersByCipher == null)
        {
            return gated;
        }

        var leasingCollectionIds = collections
            .Where(c => c.AccessRuleId.HasValue)
            .Select(c => c.Id)
            .ToHashSet();
        if (leasingCollectionIds.Count == 0)
        {
            return gated;
        }

        foreach (var (cipherId, collectionCiphers) in collectionCiphersByCipher)
        {
            if (collectionCiphers.Any() && collectionCiphers.All(cc => leasingCollectionIds.Contains(cc.CollectionId)))
            {
                gated.Add(cipherId);
            }
        }

        return gated;
    }

    public async Task<FullCipherAccess> EnsureCanMutateAsync(Guid userId, Cipher cipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        if (await IsBlockedAsync(userId, cipher.Id))
        {
            throw new NotFoundException();
        }

        return FullCipherAccess.ForCipher(cipher.Id);
    }

    public async Task<FullCipherAccess> EnsureCanMutateManyAsync(Guid userId, IEnumerable<Cipher> ciphers)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        var cipherList = ciphers as ICollection<Cipher> ?? ciphers.ToList();
        if (cipherList.Count == 0)
        {
            return FullCipherAccess.ForCiphers([]);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var leasedCipherIds = (await _accessLeaseRepository.GetManyActiveByRequesterIdAsync(userId, now))
            .Select(l => l.CipherId)
            .ToHashSet();
        var signals = AccessSignals.From(_currentContext, new DateTimeOffset(now, TimeSpan.Zero));

        foreach (var cipher in cipherList)
        {
            if (leasedCipherIds.Contains(cipher.Id))
            {
                // A valid lease overrides gating, so no resolve is needed.
                continue;
            }

            if (await _resolver.ResolveAsync(userId, cipher.Id, signals) is not null)
            {
                // Gated with no lease: refuse the whole batch, mirroring how the service hides
                // inaccessible ciphers.
                throw new NotFoundException();
            }
        }

        return FullCipherAccess.ForCiphers(cipherList.Select(c => c.Id));
    }

    public FullCipherAccess Unrestricted() =>
        // The flag-off path is unrestricted anyway; gating only ever narrows access, never widens it.
        FullCipherAccess.Unrestricted();

    /// <summary>
    /// True when the cipher is leasing-gated for the caller and they hold no valid active lease — the
    /// shared "withhold full data" condition behind both read and mutation decisions. Resolves the
    /// governing rule first so non-gated ciphers (the common case) cost no lease query.
    /// </summary>
    private async Task<bool> IsBlockedAsync(Guid userId, Guid cipherId)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var signals = AccessSignals.From(_currentContext, new DateTimeOffset(now, TimeSpan.Zero));

        if (await _resolver.ResolveAsync(userId, cipherId, signals) is null)
        {
            return false;
        }

        var activeLease = await _accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now);
        return activeLease is null;
    }
}

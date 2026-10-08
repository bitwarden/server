using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Pam.Services;
using Bit.Core.Repositories;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Services;

/// <summary>
/// The commercial <see cref="ICipherLeaseGate" />. A lease releases a gated cipher's secrets to a single read only,
/// since a bulk read would persist them on the client. Mutation follows the single read; its write-return follows the
/// bulk read.
/// </summary>
public class CipherLeaseGate : ICipherLeaseGate
{
    private readonly IFeatureService _featureService;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IGatingCollectionResolver _gatingCollectionResolver;
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly ICurrentContext _currentContext;
    private readonly TimeProvider _timeProvider;

    public CipherLeaseGate(
        IFeatureService featureService,
        IGoverningRuleResolver resolver,
        IAccessLeaseRepository accessLeaseRepository,
        IGatingCollectionResolver gatingCollectionResolver,
        ICollectionRepository collectionRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICurrentContext currentContext,
        TimeProvider timeProvider)
    {
        _featureService = featureService;
        _resolver = resolver;
        _accessLeaseRepository = accessLeaseRepository;
        _gatingCollectionResolver = gatingCollectionResolver;
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

        return await IsBlockedAsync(userId, cipher.Id, cipher.OrganizationId)
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

        var gated = await GetCallerGatedCipherIdsAsync(userId);
        return FullCipherAccess.ForCiphers(ciphers.Select(c => c.Id).Where(id => !gated.Contains(id)));
    }

    /// <remarks>Ignores leases: a lease does not unlock the echo of a mutation.</remarks>
    public async Task<FullCipherAccess?> AuthorizeWriteReturnAsync(Guid userId, Cipher cipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        return await IsGatedForCallerAsync(userId, cipher.Id, _timeProvider.GetUtcNow().UtcDateTime)
            ? null
            : FullCipherAccess.ForCipher(cipher.Id);
    }

    /// <remarks>
    /// Resolves gated-ness from the organization's collections like <see cref="AuthorizeAdminReadAsync" />, and
    /// ignores leases like <see cref="AuthorizeWriteReturnAsync" />.
    /// </remarks>
    public async Task<FullCipherAccess?> AuthorizeAdminWriteReturnAsync(
        Guid userId, Guid organizationId, Cipher cipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        var collectionIds = await _collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipher.Id);
        var leasingCollectionIds = await _gatingCollectionResolver.GetGatingCollectionIdsAsync(organizationId);
        return IsGated(collectionIds, leasingCollectionIds) ? null : FullCipherAccess.ForCipher(cipher.Id);
    }

    public async Task<FullCipherAccess> EnsureCanMutateAsync(Guid userId, Cipher cipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        if (await IsBlockedAsync(userId, cipher.Id, cipher.OrganizationId))
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

        var cipherIds = ciphers.Select(c => c.Id).Distinct().ToList();
        if (cipherIds.Count == 0)
        {
            return FullCipherAccess.ForCiphers([]);
        }

        var gated = await GetCallerGatedCipherIdsAsync(userId);
        var gatedCipherIds = cipherIds.Where(gated.Contains).ToList();
        if (gatedCipherIds.Count == 0)
        {
            return FullCipherAccess.ForCiphers(cipherIds);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var leasedCipherIds = (await _accessLeaseRepository.GetManyActiveByRequesterIdAsync(userId, now))
            .Where(l => LeaseCanRelease(l.OrganizationId))
            .Select(l => l.CipherId)
            .ToHashSet();

        // Gated with no lease; refuses the whole batch.
        if (gatedCipherIds.Any(id => !leasedCipherIds.Contains(id)))
        {
            throw new NotFoundException();
        }

        return FullCipherAccess.ForCiphers(cipherIds);
    }

    public async Task<FullCipherAccess?> AuthorizeAdminReadAsync(Guid userId, Guid organizationId, Cipher cipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        var collectionIds = await _collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipher.Id);
        var leasingCollectionIds = await _gatingCollectionResolver.GetGatingCollectionIdsAsync(organizationId);
        if (!IsGated(collectionIds, leasingCollectionIds))
        {
            return FullCipherAccess.ForCipher(cipher.Id);
        }

        if (!LeaseCanRelease(organizationId))
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var activeLease = await _accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipher.Id, now);
        return activeLease is null ? null : FullCipherAccess.ForCipher(cipher.Id);
    }

    public async Task<FullCipherAccess> AuthorizeAdminReadManyAsync(
        Guid userId,
        Guid organizationId,
        IEnumerable<Cipher> ciphers)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        var leasingCollectionIds = await _gatingCollectionResolver.GetGatingCollectionIdsAsync(organizationId);
        if (leasingCollectionIds.Count == 0)
        {
            return FullCipherAccess.ForCiphers(ciphers.Select(c => c.Id));
        }

        var collectionCiphers = await _collectionCipherRepository.GetManyByOrganizationIdAsync(organizationId);
        var gated = collectionCiphers
            .GroupBy(cc => cc.CipherId)
            .Where(g => g.All(cc => leasingCollectionIds.Contains(cc.CollectionId)))
            .Select(g => g.Key)
            .ToHashSet();

        var authorized = ciphers.Select(c => c.Id).Where(id => !gated.Contains(id));
        return FullCipherAccess.ForCiphers(authorized);
    }

    public FullCipherAccess UnrestrictedForWholeVaultExport() => FullCipherAccess.Unrestricted();

    /// <summary>
    /// A cipher reachable through <paramref name="collectionIds" /> is gated only if every one of those
    /// collections gates. A cipher also in a plain collection, or in none, is not gated.
    /// </summary>
    private static bool IsGated(ICollection<Guid> collectionIds, ISet<Guid> leasingCollectionIds) =>
        collectionIds.Count > 0 && collectionIds.All(leasingCollectionIds.Contains);

    /// <summary>Authorizes the non-gated subset of <paramref name="ciphers" /> in memory, ignoring leases.</summary>
    private FullCipherAccess BuildBulkWitness(
        IEnumerable<Cipher> ciphers,
        IEnumerable<CollectionDetails>? collections,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher)
    {
        var gated = GetGatedCipherIds(collections, collectionCiphersByCipher);
        var authorized = ciphers.Select(c => c.Id).Where(id => !gated.Contains(id));
        return FullCipherAccess.ForCiphers(authorized);
    }

    /// <summary><see cref="GetGatedCipherIds" /> over the caller's own collections and mappings.</summary>
    private async Task<ISet<Guid>> GetCallerGatedCipherIdsAsync(Guid userId)
    {
        var collections = await _collectionRepository.GetManyByUserIdAsync(userId);
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(userId);
        return GetGatedCipherIds(collections, collectionCiphers.GroupBy(cc => cc.CipherId).ToDictionary(g => g.Key));
    }

    /// <summary>
    /// The cipher ids reachable only through collections with <see cref="CollectionDetails.HasEnabledAccessRule" />.
    /// A collection read path that omits that column silently gates nothing, since Dapper does not error on it.
    /// </summary>
    private ISet<Guid> GetGatedCipherIds(
        IEnumerable<CollectionDetails>? collections,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher)
    {
        var gated = new HashSet<Guid>();
        if (!Enabled || collections == null || collectionCiphersByCipher == null)
        {
            return gated;
        }

        var leasingCollectionIds = collections
            .Where(c => c.HasEnabledAccessRule)
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

    /// <summary>True if the cipher is leasing-gated for the caller and they hold no valid active lease.</summary>
    private async Task<bool> IsBlockedAsync(Guid userId, Guid cipherId, Guid? organizationId)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (!await IsGatedForCallerAsync(userId, cipherId, now))
        {
            return false;
        }

        if (!LeaseCanRelease(organizationId))
        {
            return true;
        }

        var activeLease = await _accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now);
        return activeLease is null;
    }

    /// <summary>
    /// Whether a lease in <paramref name="organizationId" /> still authorizes anything, which requires the holder to be
    /// licensed. Claims-based, so de-licensing takes effect at the member's next token refresh.
    /// </summary>
    private bool LeaseCanRelease(Guid? organizationId) =>
        organizationId is { } id && _currentContext.AccessPam(id);

    /// <summary>Whether an enabled access rule governs the cipher for this caller.</summary>
    private async Task<bool> IsGatedForCallerAsync(Guid userId, Guid cipherId, DateTime now)
    {
        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));
        return await _resolver.ResolveAsync(userId, cipherId, signals) is not null;
    }
}

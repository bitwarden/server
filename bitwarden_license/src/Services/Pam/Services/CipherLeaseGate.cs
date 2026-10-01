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
/// The commercial <see cref="ICipherLeaseGate" />, replacing <c>UnrestrictedCipherLeaseGate</c> in builds that
/// include this library. Registered by <c>AddPamServices</c> after <c>AddBaseServices</c> so last-one-wins
/// overrides the open-source default.
/// </summary>
/// <remarks>
/// Decides only: hands back a <see cref="FullCipherAccess" /> witness (or <c>null</c>) and leaves the
/// controller to shape the response. A single read releases a gated cipher's secrets to a caller holding a
/// valid active lease; a bulk read never does, since a sync or list would leak the secret into every client's
/// local store for as long as it persists there. Mutation follows the single-read strictness (a lease-holder
/// may edit, delete, restore, or re-file), but a mutation's write-return follows the bulk strictness, since
/// the client would otherwise persist the echoed secret past the lease that justified it.
/// </remarks>
public class CipherLeaseGate : ICipherLeaseGate
{
    private readonly IFeatureService _featureService;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IAccessRuleRepository _accessRuleRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly ICurrentContext _currentContext;
    private readonly TimeProvider _timeProvider;

    public CipherLeaseGate(
        IFeatureService featureService,
        IGoverningRuleResolver resolver,
        IAccessLeaseRepository accessLeaseRepository,
        IAccessRuleRepository accessRuleRepository,
        ICollectionRepository collectionRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICurrentContext currentContext,
        TimeProvider timeProvider)
    {
        _featureService = featureService;
        _resolver = resolver;
        _accessLeaseRepository = accessLeaseRepository;
        _accessRuleRepository = accessRuleRepository;
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
            // Loading the caller's collections and mappings only inside the flag check is the point of this
            // overload's contract: the flag-off path stays query-free.
            return FullCipherAccess.Unrestricted();
        }

        var collections = await _collectionRepository.GetManyByUserIdAsync(userId);
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(userId);
        var collectionCiphersByCipher = collectionCiphers.GroupBy(cc => cc.CipherId).ToDictionary(g => g.Key);
        return BuildBulkWitness(ciphers, collections, collectionCiphersByCipher);
    }

    /// <remarks>
    /// Gated-ness is the whole test. Unlike <see cref="AuthorizeReadAsync" /> this does not go on to look for
    /// a lease, so it is also the cheaper of the two: a lease does not unlock the echo of a mutation.
    /// </remarks>
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
    /// Resolves gated-ness from the organization's collections like <see cref="AuthorizeAdminReadAsync" />,
    /// but does no lease read: a lease does not unlock a write-return for an administrator either.
    /// </remarks>
    public async Task<FullCipherAccess?> AuthorizeAdminWriteReturnAsync(
        Guid userId, Guid organizationId, Cipher cipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        var collectionIds = await _collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipher.Id);
        var leasingCollectionIds = await GetLeasingCollectionIdsAsync(organizationId);
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

        // One lease read for the whole batch, not per cipher.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var leasedCipherIds = (await _accessLeaseRepository.GetManyActiveByRequesterIdAsync(userId, now))
            .Select(l => l.CipherId)
            .ToHashSet();
        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));

        foreach (var cipherId in cipherIds)
        {
            if (leasedCipherIds.Contains(cipherId))
            {
                // A valid lease authorizes the mutation whatever rule governs the cipher, so there is
                // nothing left to resolve.
                continue;
            }

            if (await _resolver.ResolveAsync(userId, cipherId, signals) is not null)
            {
                // Gated with no lease. Refuses the whole batch rather than the one cipher, keeping a bulk
                // mutation all-or-nothing.
                throw new NotFoundException();
            }
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
        var leasingCollectionIds = await GetLeasingCollectionIdsAsync(organizationId);
        if (!IsGated(collectionIds, leasingCollectionIds))
        {
            return FullCipherAccess.ForCipher(cipher.Id);
        }

        // Gated: releases secrets only to a lease the caller actually holds, the same test the member
        // single read applies. An unlicensed administrator holds no usable lease either.
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

        var leasingCollectionIds = await GetLeasingCollectionIdsAsync(organizationId);
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

    public FullCipherAccess UnrestrictedForWholeVaultExport() =>
        // Gating only ever narrows access, never widens it, so an already-authorized context is unrestricted
        // here for the same reason it is on the flag-off path.
        FullCipherAccess.Unrestricted();

    /// <summary>
    /// The organization's collection ids gated by a currently-enabled access rule.
    /// </summary>
    /// <remarks>
    /// Resolved from the organization's rules and collections rather than the caller's, unlike the member
    /// paths, so an administrator assigned to nothing still resolves the full gated set. The organization-scoped
    /// collection read returns <see cref="Collection" />, not <see cref="CollectionDetails" />, so enabled-ness
    /// is derived from the rules directly instead of read off <c>HasEnabledAccessRule</c>.
    /// </remarks>
    private async Task<ISet<Guid>> GetLeasingCollectionIdsAsync(Guid organizationId)
    {
        var enabledRuleIds = (await _accessRuleRepository.GetManyByOrganizationIdAsync(organizationId))
            .Where(r => r.Enabled)
            .Select(r => r.Id)
            .ToHashSet();
        if (enabledRuleIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var collections = await _collectionRepository.GetManyByOrganizationIdAsync(organizationId);
        return collections
            .Where(c => c.AccessRuleId.HasValue && enabledRuleIds.Contains(c.AccessRuleId.Value))
            .Select(c => c.Id)
            .ToHashSet();
    }

    /// <summary>
    /// A cipher reachable through <paramref name="collectionIds" /> is gated only if every one of those
    /// collections gates. A cipher also in a plain collection, or in none, is not gated.
    /// </summary>
    private static bool IsGated(ICollection<Guid> collectionIds, ISet<Guid> leasingCollectionIds) =>
        collectionIds.Count > 0 && collectionIds.All(leasingCollectionIds.Contains);

    /// <summary>
    /// Authorizes the non-gated subset of <paramref name="ciphers" />, computed in-memory with no queries.
    /// Lease state is deliberately not consulted: see the strictness note on the class.
    /// </summary>
    private FullCipherAccess BuildBulkWitness(
        IEnumerable<Cipher> ciphers,
        IEnumerable<CollectionDetails>? collections,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher)
    {
        var gated = GetGatedCipherIds(collections, collectionCiphersByCipher);
        var authorized = ciphers.Select(c => c.Id).Where(id => !gated.Contains(id));
        return FullCipherAccess.ForCiphers(authorized);
    }

    /// <summary>
    /// The cipher ids reachable <em>only</em> through leasing-enabled collections, per
    /// <see cref="CollectionDetails.HasEnabledAccessRule" />.
    /// </summary>
    /// <remarks>
    /// A cipher also reachable through a plain collection is not gated, since the caller can already read it
    /// in full that way; a user-owned cipher is never gated. <c>HasEnabledAccessRule</c> is a projection added
    /// by <c>2026-08-21_00_AddPamCollectionReads.sql</c> — a collection read path that omits it silently gates
    /// nothing, since Dapper does not error on a missing column, so any new read path must carry it. A null
    /// <paramref name="collections" /> or <paramref name="collectionCiphersByCipher" /> is treated as empty.
    /// </remarks>
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

    /// <summary>
    /// True if the cipher is leasing-gated for the caller and they hold no valid active lease. Resolves the
    /// governing rule first so a non-gated cipher, the common case, costs no lease query.
    /// </summary>
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
    /// Whether a lease held in <paramref name="organizationId" /> still authorizes anything — whether the
    /// holder is still licensed.
    /// </summary>
    /// <remarks>
    /// A de-licensed member's outstanding leases stop working immediately, rather than lasting until they lapse.
    /// Checked before the lease read so an unlicensed caller costs one fewer query. Claims-based, so it takes
    /// effect at the holder's next token refresh; revoking the lease is immediate instead.
    /// </remarks>
    private bool LeaseCanRelease(Guid? organizationId) =>
        organizationId is { } id && _currentContext.AccessPam(id);

    /// <summary>
    /// Whether an enabled access rule governs the cipher for this caller — the structural half of
    /// <see cref="IsBlockedAsync" />, and on its own the whole write-return decision.
    /// </summary>
    private async Task<bool> IsGatedForCallerAsync(Guid userId, Guid cipherId, DateTime now)
    {
        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));
        return await _resolver.ResolveAsync(userId, cipherId, signals) is not null;
    }
}

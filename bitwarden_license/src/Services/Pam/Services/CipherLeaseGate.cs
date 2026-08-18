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
/// overrides the open-source default; an OSS build never reaches this type and so never gates.
/// </summary>
/// <remarks>
/// This type decides, and only decides. It shapes no response and mutates nothing: it hands back a
/// <see cref="FullCipherAccess" /> witness (or <c>null</c>) and leaves the controller to turn that into a
/// response shape. See <see cref="FullCipherAccess" /> for why that division matters.
///
/// Two decisions with deliberately different strictness live here. A single read releases a gated cipher's
/// secrets to a caller holding a valid active lease. A bulk read never does — it strips every gated cipher
/// whatever the lease state — because a sync or a list is not the act of using a credential, and letting one
/// through there would leak it into every client's local store for as long as that store lives.
///
/// The mutation decisions sit with the single read rather than the bulk one, in both their single and bulk
/// forms: a lease-holder may edit, delete, restore, or re-file the credential they hold. That asymmetry is
/// not an inconsistency — a write emits no secret, so there is nothing for strictness to protect there,
/// while refusing the holder would break the very access the lease was issued to grant.
/// </remarks>
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
            // Loading the caller's collections and mappings only inside the flag check is the point of this
            // overload's contract: the flag-off path stays query-free.
            return FullCipherAccess.Unrestricted();
        }

        var collections = await _collectionRepository.GetManyByUserIdAsync(userId);
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(userId);
        var collectionCiphersByCipher = collectionCiphers.GroupBy(cc => cc.CipherId).ToDictionary(g => g.Key);
        return BuildBulkWitness(ciphers, collections, collectionCiphersByCipher);
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

        var cipherIds = ciphers.Select(c => c.Id).Distinct().ToList();
        if (cipherIds.Count == 0)
        {
            return FullCipherAccess.ForCiphers([]);
        }

        // One lease read for the whole batch, so a caller who holds leases pays for them once rather than
        // per cipher.
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
                // Gated with no lease. Refusing the whole batch rather than the one cipher keeps a bulk
                // mutation all-or-nothing: a partially applied delete leaves the caller unable to tell what
                // happened, and mirrors how the service hides inaccessible ciphers entirely.
                throw new NotFoundException();
            }
        }

        return FullCipherAccess.ForCiphers(cipherIds);
    }

    public FullCipherAccess Unrestricted() =>
        // Gating only ever narrows access, never widens it, so an already-authorized context is unrestricted
        // here for the same reason it is on the flag-off path.
        FullCipherAccess.Unrestricted();

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
    /// The cipher ids reachable <em>only</em> through leasing-enabled collections (those carrying a
    /// <see cref="Collection.AccessRuleId" />).
    /// </summary>
    /// <remarks>
    /// "Only" is what makes this safe to compute structurally, without evaluating a rule. A cipher also
    /// reachable through a plain collection is not gated: the caller can already read it in full by that
    /// other path, so withholding it here would hide a credential leasing does not actually protect. By the
    /// same token a user-owned cipher — reachable through no collection at all — is never gated.
    ///
    /// A null <paramref name="collections" /> or <paramref name="collectionCiphersByCipher" /> means "not
    /// loaded, because the caller has no organizations", which is equivalent to empty: with no collection to
    /// reach a cipher through, nothing is gated.
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

    /// <summary>
    /// True when the cipher is leasing-gated for the caller and they hold no valid active lease — the
    /// condition behind both the single-cipher read decision ("withhold full data") and the single-cipher
    /// mutation decision ("refuse"). Resolves the governing rule first so a non-gated cipher, the common
    /// case, costs no lease query.
    /// </summary>
    private async Task<bool> IsBlockedAsync(Guid userId, Guid cipherId)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));

        if (await _resolver.ResolveAsync(userId, cipherId, signals) is null)
        {
            return false;
        }

        var activeLease = await _accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now);
        return activeLease is null;
    }
}

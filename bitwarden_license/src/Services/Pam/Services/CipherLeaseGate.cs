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
/// not an inconsistency — a mutation's request emits no secret, so there is nothing for strictness to
/// protect there, while refusing the holder would break the very access the lease was issued to grant.
///
/// What a mutation <em>returns</em> is strict again, and sits with the bulk read rather than the single one:
/// a client persists a write-return exactly as it persists a sync, so the copy would outlive the lease that
/// justified it. The caller already holds what it submitted, which makes the echo free to reduce.
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
    /// Resolves gated-ness from the organization's collections for the reason
    /// <see cref="AuthorizeAdminReadAsync" /> does, and stops there: no lease read, because a lease does not
    /// unlock a write-return for an administrator any more than it does for a member.
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

        // Gated, so this releases secrets only to a lease the caller actually holds — the same test the
        // member single read applies, and the reason an administrator assigned to nothing sees nothing:
        // a lease is issued against a collection they can reach. An administrator is subject to licensing like
        // anyone else, so an unlicensed one holds no usable lease either (see LeaseCanRelease).
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
    /// The organization's collection ids that gate: those governed by an access rule that is currently
    /// switched on.
    /// </summary>
    /// <remarks>
    /// Resolved from the organization's rules and collections rather than the caller's, which is the whole
    /// difference between the administrative decisions and the member ones. Both member paths start from
    /// the caller — <c>GetGatedCipherIds</c> from collections they are assigned to, and
    /// <c>GoverningRuleResolver.ResolveAsync</c> from
    /// <c>GetManyByUserIdCipherIdAsync</c> — so an administrator assigned to none of them resolves nothing
    /// and reads as ungated. Reusing either here would fail open for precisely the ciphers an
    /// administrator reaches without an assignment.
    ///
    /// Enabled-ness is derived from the rules directly rather than read off
    /// <see cref="CollectionDetails.HasEnabledAccessRule" />, because the organization-scoped collection
    /// read returns <see cref="Collection" />, which carries the <c>AccessRuleId</c> association but not
    /// that computed projection. Loading the rules and filtering on <c>Enabled</c> asks the same question
    /// the projection answers, and matches how <c>GoverningRuleResolver</c> drops a disabled rule.
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
    /// Whether a cipher reachable through <paramref name="collectionIds" /> is gated: it is, only when
    /// every collection it can be reached through gates. A cipher also sitting in a plain collection is
    /// readable in full by that path anyway, and one in no collection at all is user-owned.
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
    /// The cipher ids reachable <em>only</em> through leasing-enabled collections — those governed by an
    /// access rule that is currently switched on, per
    /// <see cref="CollectionDetails.HasEnabledAccessRule" />.
    /// </summary>
    /// <remarks>
    /// "Only" is what makes this safe to compute structurally, without evaluating a rule. A cipher also
    /// reachable through a plain collection is not gated: the caller can already read it in full by that
    /// other path, so withholding it here would hide a credential leasing does not actually protect. By the
    /// same token a user-owned cipher — reachable through no collection at all — is never gated.
    ///
    /// "Governed by an enabled rule", not merely carrying a <see cref="Collection.AccessRuleId" />: a
    /// disabled rule gates nothing, which is the same reading <c>GoverningRuleResolver</c> applies on the
    /// single-cipher path. Keying off the bare association instead let a cipher governed only by a disabled
    /// rule sync as partial while every other surface treated it as ungated — the item rendered with no
    /// credentials and no gating prompt either, because the banner's access-state read resolved no rule and
    /// so had nothing to offer (PM-42274). Enabled-ness is derived by the collection read paths rather than
    /// stored, so a caller supplying <paramref name="collections" /> must load them through one of those
    /// paths; a bare <see cref="Collection" /> cannot answer this.
    ///
    /// That derivation is a schema dependency, and it fails <em>open</em>: the flag arrives from a projection
    /// added by <c>2026-08-21_00_AddPamCollectionReads.sql</c>, and a read path that does not select it leaves
    /// the property at its default of false, which reads here as "no enabled rule" and so gates nothing at
    /// all. Dapper does not error on a column missing from the result set, so the only symptom is silently
    /// absent gating. The migration must therefore land before this code does, and a change to any collection
    /// read path has to carry the projection with it — the integration coverage that pins this is
    /// <c>CollectionRepositoryHasEnabledAccessRuleTests</c>, which exercises the real read rather than a
    /// substituted repository.
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
    /// True when the cipher is leasing-gated for the caller and they hold no valid active lease — the
    /// condition behind both the single-cipher read decision ("withhold full data") and the single-cipher
    /// mutation decision ("refuse"). Resolves the governing rule first so a non-gated cipher, the common
    /// case, costs no lease query.
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
    /// Whether a lease held in <paramref name="organizationId" /> still authorizes anything — that is, whether the
    /// holder is still licensed (PM-39423).
    /// </summary>
    /// <remarks>
    /// Withdrawing a member's seat withdraws the access their outstanding leases were carrying: the organization has
    /// decided this member is not to use privileged credentials, and a lease minted before that decision is not an
    /// exemption from it. Without this a de-licensed member would keep reading the credential until the lease lapsed
    /// on its own, which for a long window is indistinguishable from not having been de-licensed at all.
    ///
    /// Checked BEFORE the lease read, so an unlicensed caller costs one fewer query rather than one more.
    ///
    /// Claims-based like every other entitlement here, so it takes effect at the holder's next token refresh. An
    /// operator who needs the credential closed off sooner revokes the lease, which is immediate.
    ///
    /// A cipher with no organization cannot be gated, so this is unreachable for one — it reads as "cannot release"
    /// only because there is no subscription to license against, and the gated test above has already returned.
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

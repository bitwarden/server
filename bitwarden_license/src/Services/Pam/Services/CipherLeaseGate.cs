using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
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
/// include this library.
/// </summary>
/// <remarks>
/// A single read releases a gated cipher's secrets to a caller holding a valid active lease; a bulk read never
/// does, since a sync or list would persist the secret in the client's local store. Mutation follows the single
/// read (a lease-holder may edit, delete, restore, or move), but a mutation's write-return follows the bulk read.
/// </remarks>
public class CipherLeaseGate : ICipherLeaseGate
{
    private readonly IFeatureService _featureService;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IGatingCollectionResolver _gatingCollectionResolver;
    private readonly ICollectionRepository _collectionRepository;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;
    private readonly TimeProvider _timeProvider;

    public CipherLeaseGate(
        IFeatureService featureService,
        IGoverningRuleResolver resolver,
        IAccessLeaseRepository accessLeaseRepository,
        IGatingCollectionResolver gatingCollectionResolver,
        ICollectionRepository collectionRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ICurrentContext currentContext,
        IOrganizationAbilityCacheService organizationAbilityCacheService,
        TimeProvider timeProvider)
    {
        _featureService = featureService;
        _resolver = resolver;
        _accessLeaseRepository = accessLeaseRepository;
        _gatingCollectionResolver = gatingCollectionResolver;
        _collectionRepository = collectionRepository;
        _collectionCipherRepository = collectionCipherRepository;
        _currentContext = currentContext;
        _organizationAbilityCacheService = organizationAbilityCacheService;
        _timeProvider = timeProvider;
    }

    private bool Enabled => _featureService.IsEnabled(FeatureFlagKeys.Pam);

    public async Task<FullCipherAccess?> AuthorizeReadAsync(Guid userId, Cipher cipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        return await IsBlockedAsync(userId, cipher)
            ? null
            : FullCipherAccess.ForCipher(cipher.Id);
    }

    public async Task<FullCipherAccess> AuthorizeReadManyAsync(
        Guid userId,
        IEnumerable<Cipher> ciphers,
        IEnumerable<CollectionDetails>? collections,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        var gated = await GetGatedCipherIdsAsync(collections, collectionCiphersByCipher);
        return FullCipherAccess.ForCiphers(ciphers.Select(c => c.Id).Where(id => !gated.Contains(id)));
    }

    public async Task<FullCipherAccess> AuthorizeReadManyAsync(Guid userId, IEnumerable<Cipher> ciphers)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        var cipherList = ciphers.ToList();
        if (!await AnyCanGateAsync(cipherList))
        {
            return FullCipherAccess.ForCiphers(cipherList.Select(c => c.Id));
        }

        var gated = await GetCallerGatedCipherIdsAsync(userId);
        return FullCipherAccess.ForCiphers(cipherList.Select(c => c.Id).Where(id => !gated.Contains(id)));
    }

    public async Task<FullCipherAccess> AuthorizeReadManyAsync(
        Guid userId,
        IEnumerable<Cipher> ciphers,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        var cipherList = ciphers.ToList();
        if (!await AnyCanGateAsync(cipherList))
        {
            return FullCipherAccess.ForCiphers(cipherList.Select(c => c.Id));
        }

        return await AuthorizeReadManyAsync(userId, cipherList,
            await _collectionRepository.GetManyByUserIdAsync(userId), collectionCiphersByCipher);
    }

    /// <remarks>
    /// Ignores leases: a lease does not unlock the echo of a mutation.
    /// </remarks>
    public async Task<FullCipherAccess?> AuthorizeWriteReturnAsync(Guid userId, Cipher cipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        return await IsGatedForCallerAsync(userId, cipher, _timeProvider.GetUtcNow().UtcDateTime)
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

        if (await AdminNeedsNoLeaseAsync(organizationId))
        {
            return FullCipherAccess.ForCipher(cipher.Id);
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

        if (await IsBlockedAsync(userId, cipher))
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

        var cipherList = ciphers.ToList();
        var cipherIds = cipherList.Select(c => c.Id).Distinct().ToList();
        if (!await AnyCanGateAsync(cipherList))
        {
            return FullCipherAccess.ForCiphers(cipherIds);
        }

        var gated = await GetCallerGatedCipherIdsAsync(userId);
        await EnsureLeasedAsync(userId, cipherIds.Where(gated.Contains).ToList());
        return FullCipherAccess.ForCiphers(cipherIds);
    }

    public async Task<FullCipherAccess?> AuthorizeAdminReadAsync(Guid userId, Guid organizationId, Cipher cipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        if (await AdminNeedsNoLeaseAsync(organizationId))
        {
            return FullCipherAccess.ForCipher(cipher.Id);
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

        if (await AdminNeedsNoLeaseAsync(organizationId))
        {
            return FullCipherAccess.ForCiphers(ciphers.Select(c => c.Id));
        }

        var gated = await GetOrganizationGatedCipherIdsAsync(organizationId);
        var authorized = ciphers.Select(c => c.Id).Where(id => !gated.Contains(id));
        return FullCipherAccess.ForCiphers(authorized);
    }

    public async Task<FullCipherAccess> EnsureCanMutateAsAdminAsync(Guid userId, Cipher cipher)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        // A personal cipher is never gated.
        if (cipher.OrganizationId is not { } organizationId)
        {
            return FullCipherAccess.ForCipher(cipher.Id);
        }

        return await AuthorizeAdminReadAsync(userId, organizationId, cipher) ?? throw new NotFoundException();
    }

    public async Task<FullCipherAccess> EnsureCanMutateManyAsAdminAsync(
        Guid userId,
        Guid organizationId,
        IEnumerable<Cipher> ciphers)
    {
        if (!Enabled)
        {
            return FullCipherAccess.Unrestricted();
        }

        var cipherIds = ciphers.Select(c => c.Id).Distinct().ToList();
        if (cipherIds.Count == 0 || await AdminNeedsNoLeaseAsync(organizationId))
        {
            return FullCipherAccess.ForCiphers(cipherIds);
        }

        var gated = await GetOrganizationGatedCipherIdsAsync(organizationId);
        await EnsureLeasedAsync(userId, cipherIds.Where(gated.Contains).ToList());
        return FullCipherAccess.ForCiphers(cipherIds);
    }

    public FullCipherAccess AuthorizeAccessConnectorRotationRead(Cipher cipher) =>
        Enabled ? FullCipherAccess.ForCipher(cipher.Id) : FullCipherAccess.Unrestricted();

    public FullCipherAccess UnrestrictedForWholeVaultExport() => FullCipherAccess.Unrestricted();

    /// <summary>
    /// Whether the "/admin" decisions need no lease: the organization gates nothing, or the caller is an Owner or
    /// Admin of an organization that lets admins access all collection items.
    /// </summary>
    private async Task<bool> AdminNeedsNoLeaseAsync(Guid organizationId)
    {
        var ability = await _organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        if (!CanGate(ability))
        {
            return true;
        }

        return _currentContext.GetOrganization(organizationId) is
        { Type: OrganizationUserType.Owner or OrganizationUserType.Admin }
               && ability is { AllowAdminAccessToAllCollectionItems: true };
    }

    /// <summary>
    /// Whether an access rule can gate anything in the organization: whether it uses Privileged Controls. Rules left
    /// in an organization without it gate nothing. An unknown organization can gate, so the full check runs.
    /// </summary>
    private static bool CanGate(OrganizationAbility? ability) => ability is not { UsePam: false };

    /// <summary>
    /// The subset of <paramref name="organizationIds" /> in which an access rule can gate anything.
    /// </summary>
    private async Task<ISet<Guid>> GetGatingOrganizationIdsAsync(IEnumerable<Guid> organizationIds)
    {
        var ids = organizationIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var abilities = await _organizationAbilityCacheService.GetOrganizationAbilitiesAsync(ids);
        return ids.Where(id => CanGate(abilities.TryGetValue(id, out var ability) ? ability : null)).ToHashSet();
    }

    /// <summary>
    /// Whether any of <paramref name="ciphers" /> belongs to an organization in which an access rule can gate. A
    /// personal cipher is never gated.
    /// </summary>
    private async Task<bool> AnyCanGateAsync(IEnumerable<Cipher> ciphers)
    {
        var organizationIds = ciphers
            .Where(c => c.OrganizationId.HasValue)
            .Select(c => c.OrganizationId!.Value);
        return (await GetGatingOrganizationIdsAsync(organizationIds)).Count > 0;
    }

    /// <summary>
    /// The organization's cipher ids reachable only through leasing-enabled collections, ignoring leases.
    /// </summary>
    private async Task<ISet<Guid>> GetOrganizationGatedCipherIdsAsync(Guid organizationId)
    {
        var leasingCollectionIds = await _gatingCollectionResolver.GetGatingCollectionIdsAsync(organizationId);
        if (leasingCollectionIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var collectionCiphers = await _collectionCipherRepository.GetManyByOrganizationIdAsync(organizationId);
        return collectionCiphers
            .GroupBy(cc => cc.CipherId)
            .Where(g => g.All(cc => leasingCollectionIds.Contains(cc.CollectionId)))
            .Select(g => g.Key)
            .ToHashSet();
    }

    /// <summary>
    /// Throws <see cref="NotFoundException" /> unless the caller holds a valid active lease on every one of
    /// <paramref name="gatedCipherIds" />, refusing the whole batch.
    /// </summary>
    private async Task EnsureLeasedAsync(Guid userId, IReadOnlyCollection<Guid> gatedCipherIds)
    {
        if (gatedCipherIds.Count == 0)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var leasedCipherIds = (await _accessLeaseRepository.GetManyActiveByRequesterIdAsync(userId, now))
            .Where(l => LeaseCanRelease(l.OrganizationId))
            .Select(l => l.CipherId)
            .ToHashSet();

        if (gatedCipherIds.Any(id => !leasedCipherIds.Contains(id)))
        {
            throw new NotFoundException();
        }
    }

    /// <summary>
    /// A cipher reachable through <paramref name="collectionIds" /> is gated only if every one of those
    /// collections gates. A cipher also in a plain collection, or in none, is not gated.
    /// </summary>
    private static bool IsGated(ICollection<Guid> collectionIds, ISet<Guid> leasingCollectionIds) =>
        collectionIds.Count > 0 && collectionIds.All(leasingCollectionIds.Contains);

    /// <summary>
    /// <see cref="GetGatedCipherIdsAsync" /> over the caller's own collections and mappings, loaded in two queries.
    /// </summary>
    private async Task<ISet<Guid>> GetCallerGatedCipherIdsAsync(Guid userId)
    {
        var collections = await _collectionRepository.GetManyByUserIdAsync(userId);
        var collectionCiphers = await _collectionCipherRepository.GetManyByUserIdAsync(userId);
        return await GetGatedCipherIdsAsync(collections,
            collectionCiphers.GroupBy(cc => cc.CipherId).ToDictionary(g => g.Key));
    }

    /// <summary>
    /// The cipher ids reachable <em>only</em> through leasing-enabled collections, per
    /// <see cref="CollectionDetails.HasEnabledAccessRule" />, in organizations where an access rule can gate.
    /// </summary>
    /// <remarks>
    /// A user-owned cipher is never gated. A collection read path that omits <c>HasEnabledAccessRule</c> silently
    /// gates nothing, since Dapper does not error on a missing column. A null <paramref name="collections" /> or
    /// <paramref name="collectionCiphersByCipher" /> is treated as empty.
    /// </remarks>
    private async Task<ISet<Guid>> GetGatedCipherIdsAsync(
        IEnumerable<CollectionDetails>? collections,
        IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher)
    {
        var gated = new HashSet<Guid>();
        if (collections == null || collectionCiphersByCipher == null)
        {
            return gated;
        }

        var ruledCollections = collections.Where(c => c.HasEnabledAccessRule).ToList();
        var gatingOrganizationIds = await GetGatingOrganizationIdsAsync(ruledCollections.Select(c => c.OrganizationId));
        var leasingCollectionIds = ruledCollections
            .Where(c => gatingOrganizationIds.Contains(c.OrganizationId))
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
    /// True if the cipher is leasing-gated for the caller and they hold no valid active lease.
    /// </summary>
    private async Task<bool> IsBlockedAsync(Guid userId, Cipher cipher)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (!await IsGatedForCallerAsync(userId, cipher, now))
        {
            return false;
        }

        if (!LeaseCanRelease(cipher.OrganizationId))
        {
            return true;
        }

        var activeLease = await _accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipher.Id, now);
        return activeLease is null;
    }

    /// <summary>
    /// Whether a lease held in <paramref name="organizationId" /> still authorizes anything: whether the holder is
    /// still licensed.
    /// </summary>
    /// <remarks>
    /// Claims-based, so a de-licensed member's leases stop working at their next token refresh.
    /// </remarks>
    private bool LeaseCanRelease(Guid? organizationId) =>
        organizationId is { } id && _currentContext.AccessPam(id);

    /// <summary>
    /// Whether an enabled access rule governs the cipher for this caller. A personal cipher, or one in an organization
    /// without Privileged Controls, is never gated.
    /// </summary>
    private async Task<bool> IsGatedForCallerAsync(Guid userId, Cipher cipher, DateTime now)
    {
        if (cipher.OrganizationId is not { } organizationId
            || !CanGate(await _organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId)))
        {
            return false;
        }

        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));
        return await _resolver.ResolveAsync(userId, cipher.Id, signals) is not null;
    }
}

using Bit.Core;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Pam.Services;
using Bit.Core.Repositories;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Services;

/// <summary>
/// The commercial <see cref="ILeasingBypassAudit" />, replacing <c>NoopLeasingBypassAudit</c> in builds that include
/// this library.
/// </summary>
/// <remarks>
/// Both events are outcomes alone, written after the action: whether a collection change ungates a cipher is only
/// known once the change is stored.
/// </remarks>
public class LeasingBypassAudit : ILeasingBypassAudit
{
    private readonly IFeatureService _featureService;
    private readonly IGatingCollectionResolver _gatingCollectionResolver;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;
    private readonly TimeProvider _timeProvider;

    public LeasingBypassAudit(
        IFeatureService featureService,
        IGatingCollectionResolver gatingCollectionResolver,
        ICollectionCipherRepository collectionCipherRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        IOrganizationAbilityCacheService organizationAbilityCacheService,
        TimeProvider timeProvider)
    {
        _featureService = featureService;
        _gatingCollectionResolver = gatingCollectionResolver;
        _collectionCipherRepository = collectionCipherRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _organizationAbilityCacheService = organizationAbilityCacheService;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlySet<Guid>> GetGatedCipherIdsAsync(Guid organizationId, IEnumerable<Guid> cipherIds)
    {
        var ids = cipherIds.ToHashSet();
        if (ids.Count == 0 || !_featureService.IsEnabled(FeatureFlagKeys.Pam)
            || !CipherLeaseGate.CanGate(await _organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId)))
        {
            return new HashSet<Guid>();
        }

        var gatingCollectionIds = await _gatingCollectionResolver.GetGatingCollectionIdsAsync(organizationId);
        if (gatingCollectionIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var collectionIdsByCipher = await GetCollectionIdsByCipherAsync(organizationId, ids);
        return collectionIdsByCipher
            .Where(entry => CipherLeaseGate.IsGated(entry.Value, gatingCollectionIds))
            .Select(entry => entry.Key)
            .ToHashSet();
    }

    public async Task AuditUngatedAsync(Guid organizationId, Guid actingUserId, IReadOnlySet<Guid> gatedCipherIds)
    {
        if (gatedCipherIds.Count == 0)
        {
            return;
        }

        var gatingCollectionIds = await _gatingCollectionResolver.GetGatingCollectionIdsAsync(organizationId);
        var collectionIdsByCipher = await GetCollectionIdsByCipherAsync(organizationId, gatedCipherIds);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var cipherId in gatedCipherIds)
        {
            var collectionIds = collectionIdsByCipher.GetValueOrDefault(cipherId) ?? [];
            if (CipherLeaseGate.IsGated(collectionIds, gatingCollectionIds))
            {
                continue;
            }

            await _accessAuditEventEmitter.EmitAsync(new AccessAuditEventData
            {
                Kind = AccessAuditEventKind.CredentialUngated,
                OccurredDate = now,
                OrganizationId = organizationId,
                ActorId = actingUserId,
                CipherId = cipherId,
                // The collection that now reaches the cipher ungated; none when the change left it in no collection.
                CollectionId = collectionIds
                    .Where(id => !gatingCollectionIds.Contains(id))
                    .Order()
                    .Select(id => (Guid?)id)
                    .FirstOrDefault(),
            });
        }
    }

    public async Task AuditWholeVaultExportAsync(Guid organizationId, Guid actingUserId,
        IEnumerable<Guid> exportedCipherIds)
    {
        var gatedCipherIds = await GetGatedCipherIdsAsync(organizationId, exportedCipherIds);
        if (gatedCipherIds.Count == 0)
        {
            return;
        }

        await _accessAuditEventEmitter.EmitAsync(new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.VaultExported,
            OccurredDate = _timeProvider.GetUtcNow().UtcDateTime,
            OrganizationId = organizationId,
            ActorId = actingUserId,
        });
    }

    /// <summary>
    /// A single cipher reads its own collections; more read the organization's once. A cipher in no collection is
    /// absent.
    /// </summary>
    private async Task<Dictionary<Guid, ICollection<Guid>>> GetCollectionIdsByCipherAsync(
        Guid organizationId, IReadOnlySet<Guid> cipherIds)
    {
        if (cipherIds.Count == 1)
        {
            var cipherId = cipherIds.Single();
            var collectionIds = await _collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipherId);
            var byCipher = new Dictionary<Guid, ICollection<Guid>>();
            if (collectionIds.Count > 0)
            {
                byCipher[cipherId] = collectionIds;
            }

            return byCipher;
        }

        return (await _collectionCipherRepository.GetManyByOrganizationIdAsync(organizationId))
            .Where(cc => cipherIds.Contains(cc.CipherId))
            .GroupBy(cc => cc.CipherId)
            .ToDictionary(g => g.Key, g => (ICollection<Guid>)g.Select(cc => cc.CollectionId).ToList());
    }
}

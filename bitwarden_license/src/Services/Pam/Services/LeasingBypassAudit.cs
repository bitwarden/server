using Bit.Core.Pam.Services;
using Bit.Core.Repositories;
using Bit.Pam.Enums;
using Bit.Pam.Models;

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
    private readonly ICipherLeaseGate _cipherLeaseGate;
    private readonly IGatingCollectionResolver _gatingCollectionResolver;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public LeasingBypassAudit(
        ICipherLeaseGate cipherLeaseGate,
        IGatingCollectionResolver gatingCollectionResolver,
        ICollectionCipherRepository collectionCipherRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _cipherLeaseGate = cipherLeaseGate;
        _gatingCollectionResolver = gatingCollectionResolver;
        _collectionCipherRepository = collectionCipherRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task AuditCollectionChangeAsync(Guid organizationId, Guid actingUserId, IEnumerable<Guid> cipherIds,
        Func<Task> change)
    {
        var gatedBefore = await _cipherLeaseGate.GetOrganizationGatedCipherIdsAsync(organizationId, cipherIds);
        await change();
        if (gatedBefore.Count == 0)
        {
            return;
        }

        var gatedAfter = await _cipherLeaseGate.GetOrganizationGatedCipherIdsAsync(organizationId, gatedBefore);
        var ungated = gatedBefore.Where(id => !gatedAfter.Contains(id)).ToList();
        if (ungated.Count == 0)
        {
            return;
        }

        // Every collection an ungated cipher was in before gated, so each ungated collection it is in now was added.
        var gatingCollectionIds = await _gatingCollectionResolver.GetGatingCollectionIdsAsync(organizationId);
        var addedByCipher = (await _collectionCipherRepository.GetManyByCipherIdsAsync(ungated))
            .Where(cc => !gatingCollectionIds.Contains(cc.CollectionId))
            .GroupBy(cc => cc.CipherId)
            .ToDictionary(g => g.Key, g => g.Select(cc => cc.CollectionId).ToList());
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var cipherId in ungated)
        {
            await _accessAuditEventEmitter.EmitAsync(new AccessAuditEventData
            {
                Kind = AccessAuditEventKind.CredentialUngated,
                OccurredDate = now,
                OrganizationId = organizationId,
                ActorId = actingUserId,
                CipherId = cipherId,
                // Named only when the change added exactly one, so the event never points at the wrong collection.
                CollectionId = addedByCipher.GetValueOrDefault(cipherId) is [var added] ? added : null,
            });
        }
    }

    public async Task AuditWholeVaultExportAsync(Guid organizationId, Guid actingUserId,
        IEnumerable<Guid> exportedCipherIds)
    {
        var gatedCipherIds = await _cipherLeaseGate.GetOrganizationGatedCipherIdsForExportAsync(organizationId);
        if (!exportedCipherIds.Any(gatedCipherIds.Contains))
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
}

using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Jobs;

/// <inheritdoc cref="IPamLeaseExpirySweepService" />
public class PamLeaseExpirySweepService : IPamLeaseExpirySweepService
{
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly IHandleAccessGrantEndedCommand _handleAccessGrantEndedCommand;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PamLeaseExpirySweepService> _logger;

    public PamLeaseExpirySweepService(
        IAccessLeaseRepository accessLeaseRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        IHandleAccessGrantEndedCommand handleAccessGrantEndedCommand,
        TimeProvider timeProvider,
        ILogger<PamLeaseExpirySweepService> logger)
    {
        _accessLeaseRepository = accessLeaseRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _handleAccessGrantEndedCommand = handleAccessGrantEndedCommand;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task SweepAsync()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiredLeases = await _accessLeaseRepository.ExpireDueAsync(now);

        foreach (var lease in expiredLeases)
        {
            try
            {
                // Machinery event: single Outcome-phase, no human actor -- mirrors RevokeAccessLeaseCommand's
                // LeaseRevoked construction, adapted for a lease that ended on its own rather than by a decision.
                var audit = new AccessAuditEventData
                {
                    Kind = AccessAuditEventKind.LeaseExpired,
                    OccurredAt = now,
                    OrganizationId = lease.OrganizationId,
                    ActorId = null,
                    RequesterId = lease.RequesterId,
                    CollectionId = lease.CollectionId,
                    CipherId = lease.CipherId,
                    AccessLeaseId = lease.Id,
                    LeaseNotBefore = lease.NotBefore,
                    LeaseNotAfter = lease.NotAfter,
                };
                await _accessAuditEventEmitter.EmitAsync(audit);

                // Self-gates on the PamRotation flag -- safe to call unconditionally here, the same as the
                // RevokeAccessLeaseCommand hook.
                await _handleAccessGrantEndedCommand.HandleAsync(lease.CipherId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PamLeaseExpirySweepService: failed to process expired lease {AccessLeaseId}.", lease.Id);
            }
        }
    }
}

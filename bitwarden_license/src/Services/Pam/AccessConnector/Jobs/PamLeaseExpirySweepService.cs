using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Jobs;

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
                // Emitting the audit and firing the access-end trigger are independent, so they get independent
                // try blocks: ExpireDueAsync has already journaled the whole batch as swept, so a lease is never
                // returned twice. Sharing one block meant an audit-store hiccup silently swallowed the rotation
                // trigger for that lease -- and RotateOnAccessEnd is the control that stops a credential the user
                // just held from staying valid.
                try
                {
                    await _accessAuditEventEmitter.EmitAsync(audit);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "PamLeaseExpirySweepService: failed to emit the audit event for expired lease {AccessLeaseId}.",
                        lease.Id);
                }

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

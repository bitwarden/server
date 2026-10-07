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
                // Machinery event: single Outcome-phase, no human actor.
                var audit = new AccessAuditEventData
                {
                    Kind = AccessAuditEventKind.LeaseExpired,
                    OccurredDate = now,
                    OrganizationId = lease.OrganizationId,
                    ActorId = null,
                    RequesterId = lease.RequesterId,
                    CollectionId = lease.CollectionId,
                    CipherId = lease.CipherId,
                    AccessLeaseId = lease.Id,
                    LeaseNotBefore = lease.NotBefore,
                    LeaseNotAfter = lease.NotAfter,
                };
                // A separate try, so an audit failure cannot skip the rotation trigger that retires the credential
                // the user held.
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

                // Gates itself on the PamAccessConnector flag, so it is safe to call unconditionally.
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

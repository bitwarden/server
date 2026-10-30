using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class RevokeAccessLeaseCommand : IRevokeAccessLeaseCommand
{
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly IRequesterNotifier _requesterNotifier;
    private readonly ILeaseRevokedMailNotifier _leaseRevokedMailNotifier;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly IHandleAccessGrantEndedCommand _handleAccessGrantEndedCommand;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RevokeAccessLeaseCommand> _logger;

    public RevokeAccessLeaseCommand(
        IAccessLeaseRepository accessLeaseRepository,
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IApproverInboxNotifier approverInboxNotifier,
        IRequesterNotifier requesterNotifier,
        ILeaseRevokedMailNotifier leaseRevokedMailNotifier,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        IHandleAccessGrantEndedCommand handleAccessGrantEndedCommand,
        TimeProvider timeProvider,
        ILogger<RevokeAccessLeaseCommand> logger)
    {
        _accessLeaseRepository = accessLeaseRepository;
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _approverInboxNotifier = approverInboxNotifier;
        _requesterNotifier = requesterNotifier;
        _leaseRevokedMailNotifier = leaseRevokedMailNotifier;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _handleAccessGrantEndedCommand = handleAccessGrantEndedCommand;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RevokeAsync(Guid userId, Guid leaseId, string? reason)
    {
        var lease = await _accessLeaseRepository.GetByIdAsync(leaseId);

        // The holder, or anyone who can Manage the lease's collection, may end it early. 404 for both missing and
        // not authorized, so a caller can't probe for leases they can't touch.
        var isHolder = lease is not null && lease.RequesterId == userId;
        if (lease is null ||
            (!isHolder && !await _approverCollectionAccessQuery.CanManageCollectionAsync(userId, lease.CollectionId)))
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Expiry is never written, so ending a lapsed lease would record its natural end as an early one.
        if (!lease.IsLive(now))
        {
            throw new ConflictException("This lease is not active.");
        }

        var endAction = isHolder ? AccessLeaseAction.Cancelled : AccessLeaseAction.Revoked;

        // The reason has no dedicated column; it is preserved as a human decision against the originating request.
        var auditDecision = new AccessDecision
        {
            AccessRequestId = lease.AccessRequestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = userId,
            Verdict = AccessDecisionVerdict.Deny,
            Comment = string.IsNullOrWhiteSpace(reason) ? null : reason,
            CreationDate = now,
        };
        auditDecision.SetNewId();

        // Ending by the holder and revocation by an operator are both LeaseRevoked.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.LeaseRevoked,
            OccurredDate = now,
            OrganizationId = lease.OrganizationId,
            ActorId = userId,
            RequesterId = lease.RequesterId,
            CollectionId = lease.CollectionId,
            CipherId = lease.CipherId,
            AccessRequestId = lease.AccessRequestId,
            AccessLeaseId = lease.Id,
            LeaseNotBefore = lease.NotBefore,
            LeaseNotAfter = lease.NotAfter,
            Detail = string.IsNullOrWhiteSpace(reason) ? null : reason,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        await _accessLeaseRepository.RevokeAsync(lease, endAction, auditDecision, now);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

        // The lease has already ended, so a failure here is logged rather than failing the revoke.
        try
        {
            await _handleAccessGrantEndedCommand.HandleAsync(lease.CipherId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to trigger the rotation access-end handler for cipher {CipherId} after revoking lease {AccessLeaseId}.",
                lease.CipherId, lease.Id);
        }

        await _approverInboxNotifier.NotifyCollectionApproversAsync(lease.CollectionId);
        await _requesterNotifier.NotifyRequesterAsync(lease.RequesterId);
        await _leaseRevokedMailNotifier.NotifyLeaseEndedAsync(lease, endAction);
    }
}

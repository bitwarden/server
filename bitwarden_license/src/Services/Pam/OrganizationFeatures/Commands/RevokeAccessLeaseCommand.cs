using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class RevokeAccessLeaseCommand : IRevokeAccessLeaseCommand
{
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly IRequesterNotifier _requesterNotifier;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly IHandleAccessGrantEndedCommand _handleAccessGrantEndedCommand;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RevokeAccessLeaseCommand> _logger;

    public RevokeAccessLeaseCommand(
        IAccessLeaseRepository accessLeaseRepository,
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IApproverInboxNotifier approverInboxNotifier,
        IRequesterNotifier requesterNotifier,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        IHandleAccessGrantEndedCommand handleAccessGrantEndedCommand,
        TimeProvider timeProvider,
        ILogger<RevokeAccessLeaseCommand> logger)
    {
        _accessLeaseRepository = accessLeaseRepository;
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _approverInboxNotifier = approverInboxNotifier;
        _requesterNotifier = requesterNotifier;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _handleAccessGrantEndedCommand = handleAccessGrantEndedCommand;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RevokeAsync(Guid userId, Guid leaseId, string? reason)
    {
        var lease = await _accessLeaseRepository.GetByIdAsync(leaseId);

        // Who may end a lease early: the lease's own holder (ending their own access), or anyone who can Manage its
        // collection (a managing approver or org admin). The outcome status records the manner — the holder ending
        // their own access settles to Cancelled, an operator ending it settles to Revoked — while RevokedBy records the
        // actor either way. 404 covers both missing and not-authorized, so a caller can't probe for leases they can't touch.
        var isHolder = lease is not null && lease.RequesterId == userId;
        if (lease is null ||
            (!isHolder && !await _approverCollectionAccessQuery.CanManageCollectionAsync(userId, lease.CollectionId)))
        {
            throw new NotFoundException();
        }

        if (lease.Status != AccessLeaseStatus.Active)
        {
            throw new ConflictException("This lease is not active.");
        }

        var endStatus = isHolder ? AccessLeaseStatus.Cancelled : AccessLeaseStatus.Revoked;

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // The reason has no dedicated column, so it is preserved as a human decision against the originating request.
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

        // audit (before/after): record the revoke attempt, then the outcome around the point of no return. A holder
        // ending their own lease and an operator revoking both settle to the single LeaseRevoked kind.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.LeaseRevoked,
            OccurredAt = now,
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

        await _accessLeaseRepository.RevokeAsync(lease, endStatus, auditDecision, now);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

        // Both a holder self-end and an operator revoke are grant-ends (spec RotateOnAccessEnd /
        // RaiseManualObligationOnAccessEnd); the handler self-gates on the PamRotation flag. A failure here must
        // never fail the revoke itself -- the lease has already ended -- so it is logged and swallowed.
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

        // The active lease just drained; tell every approver of this collection to re-fetch.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(lease.CollectionId);

        // Tell the lease holder their access ended, so an open cipher re-locks and the banner/badges drop the lease
        // — whether an operator revoked it or the holder ended it from another device.
        await _requesterNotifier.NotifyRequesterAsync(lease.RequesterId);
    }
}

using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class RevokeAccessLeaseCommand : IRevokeAccessLeaseCommand
{
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public RevokeAccessLeaseCommand(
        IAccessLeaseRepository accessLeaseRepository,
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _accessLeaseRepository = accessLeaseRepository;
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task RevokeAsync(Guid userId, Guid leaseId, string? reason)
    {
        var lease = await _accessLeaseRepository.GetByIdAsync(leaseId);

        // Who may end a lease early: the holder, or anyone who can Manage its collection. The holder ending their
        // own access settles to Cancelled; an operator ending it settles to Revoked. 404 covers both missing and
        // not-authorized so a caller can't probe for leases they can't touch.
        var isHolder = lease is not null && lease.RequesterId == userId;
        if (lease is null ||
            (!isHolder && !await _approverCollectionAccessQuery.CanManageCollectionAsync(userId, lease.CollectionId)))
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // A lease whose window has closed carries no early end; nothing ever writes expiry, so ending one here
        // would misrepresent a lease that ran out on its own as an operator action.
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

        // A holder self-end and an operator revoke both settle to the single LeaseRevoked kind.
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
    }
}

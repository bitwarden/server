using Bit.Commercial.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Commercial.Pam.Services;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;

namespace Bit.Commercial.Pam.OrganizationFeatures.Commands;

public class RevokeAccessLeaseCommand : IRevokeAccessLeaseCommand
{
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly IRequesterNotifier _requesterNotifier;
    private readonly TimeProvider _timeProvider;

    public RevokeAccessLeaseCommand(
        IAccessLeaseRepository accessLeaseRepository,
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IApproverInboxNotifier approverInboxNotifier,
        IRequesterNotifier requesterNotifier,
        TimeProvider timeProvider)
    {
        _accessLeaseRepository = accessLeaseRepository;
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _approverInboxNotifier = approverInboxNotifier;
        _requesterNotifier = requesterNotifier;
        _timeProvider = timeProvider;
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

        await _accessLeaseRepository.RevokeAsync(lease, endStatus, auditDecision, now);

        // The active lease just drained; tell every approver of this collection to re-fetch.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(lease.CollectionId);

        // Tell the lease holder their access ended, so an open cipher re-locks and the banner/badges drop the lease
        // — whether an operator revoked it or the holder ended it from another device.
        await _requesterNotifier.NotifyRequesterAsync(lease.RequesterId);
    }
}

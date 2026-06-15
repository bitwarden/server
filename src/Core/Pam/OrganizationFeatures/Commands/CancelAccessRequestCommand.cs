using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;

namespace Bit.Core.Pam.OrganizationFeatures.Commands;

public class CancelAccessRequestCommand : ICancelAccessRequestCommand
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly TimeProvider _timeProvider;

    public CancelAccessRequestCommand(
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IApproverInboxNotifier approverInboxNotifier,
        TimeProvider timeProvider)
    {
        _accessRequestRepository = accessRequestRepository;
        _accessLeaseRepository = accessLeaseRepository;
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _approverInboxNotifier = approverInboxNotifier;
        _timeProvider = timeProvider;
    }

    public async Task CancelAsync(Guid userId, Guid requestId)
    {
        var request = await _accessRequestRepository.GetByIdAsync(requestId);

        // 404 when the request is missing or the caller is neither its requester nor a managing approver, so the
        // caller can't probe for requests they have no business seeing. Mirrors the inbox/decide surfaces.
        if (request is null)
        {
            throw new NotFoundException();
        }

        var isRequester = request.RequesterId == userId;
        var isManager = !isRequester
            && await _approverCollectionAccessQuery.CanManageCollectionAsync(userId, request.CollectionId);
        if (!isRequester && !isManager)
        {
            throw new NotFoundException();
        }

        // Only a request that has not produced a lease can be cancelled: still Pending, or Approved that the requester
        // has not yet activated. Anything else (denied/cancelled/expired) is terminal; surfaced as a conflict so the
        // client refreshes. The stored procs additionally guard the transition to stay race-safe.
        if (request.Status is not (AccessRequestStatus.Pending or AccessRequestStatus.Approved))
        {
            throw new ConflictException("This request has already been resolved.");
        }

        // An approved request that has minted a lease is governed by that lease, not the request: end it via lease
        // revoke while active, and once the lease has ended the request is terminal history.
        var lease = await _accessLeaseRepository.GetByAccessRequestIdAsync(requestId);
        if (lease is not null)
        {
            throw lease.Status == AccessLeaseStatus.Active
                ? new ConflictException("This request has an active lease; revoke the lease instead.")
                : new ConflictException("This request has already been resolved.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (isRequester)
        {
            // The requester withdraws their own request: Cancelled, no decision recorded. A user who is both the
            // requester and a manager takes this branch when cancelling their own request.
            await _accessRequestRepository.CancelAsync(request.Id, now);
        }
        else
        {
            // A managing approver retracts the request: Denied, recorded as a human Deny decision so the audit trail
            // names the approver — mirrors RevokeAccessLeaseCommand.
            var decision = new AccessDecision
            {
                AccessRequestId = request.Id,
                DeciderKind = AccessDeciderKind.Human,
                ApproverId = userId,
                Verdict = AccessDecisionVerdict.Deny,
                Comment = null,
                CreationDate = now,
            };
            decision.SetNewId();
            await _accessRequestRepository.CancelWithDecisionAsync(request, decision, now);
        }

        // The request just left the pending/approved set; tell every approver of this collection to re-fetch so it
        // drops out of their inbox. Mirrors decide.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(request.CollectionId);
    }
}

using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class CancelAccessRequestCommand : ICancelAccessRequestCommand
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly TimeProvider _timeProvider;

    public CancelAccessRequestCommand(
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        TimeProvider timeProvider)
    {
        _accessRequestRepository = accessRequestRepository;
        _accessLeaseRepository = accessLeaseRepository;
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _timeProvider = timeProvider;
    }

    public async Task CancelAsync(Guid userId, Guid requestId)
    {
        var request = await _accessRequestRepository.GetByIdAsync(requestId);

        // 404 when the request is missing or the caller is neither its requester nor a managing approver.
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

        // Only an open request, or an approved one not yet activated, can be cancelled.
        if (request.Action is not (AccessRequestAction.None or AccessRequestAction.Approved))
        {
            throw new ConflictException("This request has already been resolved.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // A minted lease governs the request. Checked before the window guard, since an extension can keep the lease
        // live after the request's window lapses.
        var lease = await _accessLeaseRepository.GetByAccessRequestIdAsync(requestId);
        if (lease is not null)
        {
            throw lease.IsLive(now)
                ? new ConflictException("This request has an active lease; revoke the lease instead.")
                : new ConflictException("This request has already been resolved.");
        }

        // A lapsed window reads as Expired; cancelling must not restamp it.
        if (!request.IsWindowOpen(now))
        {
            throw new ConflictException("This request's window has already ended.");
        }

        bool cancelled;
        if (isRequester)
        {
            // The requester withdraws their own request: Cancelled, no decision recorded.
            cancelled = await _accessRequestRepository.CancelAsync(request.Id, now);
        }
        else
        {
            // A managing approver retracts the request: Denied, with a human Deny decision naming the approver.
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
            cancelled = await _accessRequestRepository.CancelWithDecisionAsync(request, decision, now);
        }

        if (!cancelled)
        {
            throw new ConflictException("This request has already been resolved.");
        }
    }
}

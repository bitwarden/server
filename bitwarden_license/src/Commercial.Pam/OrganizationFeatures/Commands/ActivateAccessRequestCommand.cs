using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Pam.Repositories;
using Bit.Pam.Services;

namespace Bit.Commercial.Pam.OrganizationFeatures.Commands;

public class ActivateAccessRequestCommand : IActivateAccessRequestCommand
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly IRequesterNotifier _requesterNotifier;
    private readonly ISingleActiveLeaseEvaluator _singleActiveLeaseEvaluator;
    private readonly TimeProvider _timeProvider;

    public ActivateAccessRequestCommand(
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        IApproverInboxNotifier approverInboxNotifier,
        IRequesterNotifier requesterNotifier,
        ISingleActiveLeaseEvaluator singleActiveLeaseEvaluator,
        TimeProvider timeProvider)
    {
        _accessRequestRepository = accessRequestRepository;
        _accessLeaseRepository = accessLeaseRepository;
        _approverInboxNotifier = approverInboxNotifier;
        _requesterNotifier = requesterNotifier;
        _singleActiveLeaseEvaluator = singleActiveLeaseEvaluator;
        _timeProvider = timeProvider;
    }

    public async Task<AccessLease> ActivateAsync(Guid userId, Guid requestId)
    {
        var request = await _accessRequestRepository.GetByIdAsync(requestId);

        // 404 for both missing and someone else's request, so the caller can't probe for requests they don't own.
        if (request is null || request.RequesterId != userId)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Activation is idempotent while the produced lease is live (double-click, a second tab racing the
        // auto-activating open flow); a revoked or lapsed lease is final — a request authorizes access at most once.
        var existing = await _accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        if (existing is not null)
        {
            if (existing.Status == AccessLeaseStatus.Active && existing.NotAfter > now)
            {
                return existing;
            }
            throw new ConflictException("This request's access has already been used and is no longer active.");
        }

        if (request.Status != AccessRequestStatus.Approved)
        {
            throw new ConflictException(request.Status == AccessRequestStatus.Pending
                ? "This request has not been approved yet."
                : "This request can no longer be activated.");
        }

        if (request.NotBefore > now)
        {
            throw new BadRequestException("The approved access window has not started yet.");
        }

        if (request.NotAfter <= now)
        {
            throw new BadRequestException("The approved access window has already ended.");
        }

        var lease = new AccessLease
        {
            AccessRequestId = request.Id,
            OrganizationId = request.OrganizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            Status = AccessLeaseStatus.Active,
            // Activation mints the window the approver approved, exactly as the old approval-time path did; the
            // creation date is the activation audit timestamp (no decision row is written — approval was the
            // decision).
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            CreationDate = now,
        };
        lease.SetNewId();

        // The per-cipher singleton binds only when every path the caller reaches the cipher through is governed by a
        // singleton rule; an escape path leaves them unconstrained. The mint proc enforces it under a range lock.
        var enforceSingleActiveLease = await _singleActiveLeaseEvaluator.AppliesAsync(userId, request.CipherId);

        var outcome = await _accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, enforceSingleActiveLease);

        if (outcome == AccessLeaseMintOutcome.SingleActiveLeaseConflict)
        {
            throw new ConflictException("Another active lease exists for this item. Try again once it ends.");
        }

        if (outcome == AccessLeaseMintOutcome.PreconditionFailed)
        {
            // Lost a race: the guarded insert re-checks every precondition, so a miss means another activation won
            // or the request changed underneath us. If the winner's lease is live, activation still succeeded from
            // this caller's point of view.
            var winner = await _accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
            if (winner is { Status: AccessLeaseStatus.Active } && winner.NotAfter > now)
            {
                return winner;
            }
            throw new ConflictException("This request can no longer be activated.");
        }

        // The approver's history row just flipped approved -> activated and gained a revocable lease; tell every
        // approver of this collection to re-fetch, mirroring decide and revoke.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(request.CollectionId);

        // Tell the requester's other devices the approved request just minted a lease, so their "My requests" view
        // and any open partial cipher pick up the live lease without a manual refresh.
        await _requesterNotifier.NotifyRequesterAsync(request.RequesterId);

        return lease;
    }
}

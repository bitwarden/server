using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;

namespace Bit.Core.Pam.OrganizationFeatures.Commands;

public class DecideLeaseRequestCommand : IDecideLeaseRequestCommand
{
    private readonly ILeaseRequestRepository _leaseRequestRepository;
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly TimeProvider _timeProvider;

    public DecideLeaseRequestCommand(
        ILeaseRequestRepository leaseRequestRepository,
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IApproverInboxNotifier approverInboxNotifier,
        TimeProvider timeProvider)
    {
        _leaseRequestRepository = leaseRequestRepository;
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _approverInboxNotifier = approverInboxNotifier;
        _timeProvider = timeProvider;
    }

    public async Task<InboxLeaseRequestDetails> DecideAsync(Guid userId, Guid requestId, LeaseDecisionSubmission submission)
    {
        var request = await _leaseRequestRepository.GetByIdAsync(requestId);

        // 404 for both missing and not-visible, so the caller can't probe for requests they don't manage.
        if (request is null || !await _approverCollectionAccessQuery.CanManageCollectionAsync(userId, request.CollectionId))
        {
            throw new NotFoundException();
        }

        if (request.Status != LeaseRequestStatus.Pending)
        {
            throw new ConflictException("This request has already been resolved.");
        }

        // Self-approval is blocked server-side even though the client disables the buttons. Surfaced as 400 rather
        // than 403 because Bitwarden clients treat 403 as a forced logout.
        if (request.RequesterId == userId)
        {
            throw new BadRequestException("You cannot decide your own request.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var approved = submission.Verdict == LeaseDecisionVerdict.Approve;
        var status = approved ? LeaseRequestStatus.Approved : LeaseRequestStatus.Denied;

        var decision = new LeaseDecision
        {
            LeaseRequestId = request.Id,
            DeciderKind = LeaseDecisionKind.Human,
            ApproverId = userId,
            Decision = submission.Verdict,
            Comment = string.IsNullOrWhiteSpace(submission.Comment) ? null : submission.Comment,
            CreationDate = now,
        };
        decision.SetNewId();

        // Approval mints the active lease that actually authorizes access, spanning the request's approved window.
        // Without it the requester would be Approved but hold no lease, so pre-check and the cipher read would both
        // deny them. A denial creates no lease.
        Lease? lease = null;
        if (approved)
        {
            lease = new Lease
            {
                LeaseRequestId = request.Id,
                OrganizationId = request.OrganizationId,
                CollectionId = request.CollectionId,
                CipherId = request.CipherId,
                RequesterId = request.RequesterId,
                Status = LeaseStatus.Active,
                NotBefore = request.NotBefore,
                NotAfter = request.NotAfter,
                CreationDate = now,
            };
            lease.SetNewId();
        }

        await _leaseRequestRepository.ResolveWithDecisionAsync(request, decision, status, lease, now);

        // The request just left the pending queue; tell every approver of this collection to re-fetch.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(request.CollectionId);

        // The client repaints the row from Status, ResolvedAt, and ResolverComment, so those must be accurate; the
        // denormalized display fields already live on the client's existing row. Project from what we just wrote
        // rather than re-reading.
        return new InboxLeaseRequestDetails
        {
            Id = request.Id,
            ExtensionOfLeaseId = request.LeaseId,
            OrganizationId = request.OrganizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            Reason = request.Reason,
            Status = status,
            CreationDate = request.CreationDate,
            ResolvedDate = now,
            ResolverId = decision.ApproverId,
            ResolverComment = decision.Comment,
        };
    }
}

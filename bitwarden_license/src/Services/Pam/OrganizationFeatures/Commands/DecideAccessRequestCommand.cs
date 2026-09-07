using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class DecideAccessRequestCommand : IDecideAccessRequestCommand
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly IRequesterNotifier _requesterNotifier;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public DecideAccessRequestCommand(
        IAccessRequestRepository accessRequestRepository,
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IApproverInboxNotifier approverInboxNotifier,
        IRequesterNotifier requesterNotifier,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _accessRequestRepository = accessRequestRepository;
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _approverInboxNotifier = approverInboxNotifier;
        _requesterNotifier = requesterNotifier;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task<AccessRequestDetails> DecideAsync(Guid userId, Guid requestId, AccessDecisionSubmission submission)
    {
        var request = await _accessRequestRepository.GetByIdAsync(requestId);

        // 404 for both missing and not-visible, so the caller can't probe for requests they don't manage.
        if (request is null || !await _approverCollectionAccessQuery.CanManageCollectionAsync(userId, request.CollectionId))
        {
            throw new NotFoundException();
        }

        // An extension is decided at creation (RequestLeaseExtensionCommand), so no approver route reaches one; this
        // guard is a deliberate backstop against reopening the second-lease hole that ordering closes.
        if (request.ExtensionOfLeaseId is not null)
        {
            throw new BadRequestException("An extension is approved when it is requested and cannot be decided.");
        }

        if (request.Action != AccessRequestAction.None)
        {
            throw new ConflictException("This request has already been resolved.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // A lapsed window is derived Expired everywhere it's read; neither verdict may restamp it, so this is 409
        // like already-resolved.
        if (!request.IsWindowOpen(now))
        {
            throw new ConflictException("This request's window has already ended.");
        }

        // 400 rather than 403: Bitwarden clients treat 403 as a forced logout.
        if (request.RequesterId == userId)
        {
            throw new BadRequestException("You cannot decide your own request.");
        }

        var approved = submission.Verdict == AccessDecisionVerdict.Approve;

        // A denial's reason feeds the requester notification and the audit record, and there's no later chance to add it.
        if (!approved && string.IsNullOrWhiteSpace(submission.Comment))
        {
            throw new BadRequestException("A reason is required when denying a request.");
        }

        var action = approved ? AccessRequestAction.Approved : AccessRequestAction.Denied;

        var decision = new AccessDecision
        {
            AccessRequestId = request.Id,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = userId,
            Verdict = submission.Verdict,
            Comment = string.IsNullOrWhiteSpace(submission.Comment) ? null : submission.Comment,
            CreationDate = now,
        };
        decision.SetNewId();

        // Audit before/after: the verdict is known up front, so both phases carry the resulting kind.
        var auditKind = approved ? AccessAuditEventKind.RequestApproved : AccessAuditEventKind.RequestDenied;
        var audit = new AccessAuditEventData
        {
            Kind = auditKind,
            OccurredAt = now,
            OrganizationId = request.OrganizationId,
            ActorId = userId,
            RequesterId = request.RequesterId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            AccessRequestId = request.Id,
            Detail = decision.Comment,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // Approval records the verdict only; the lease is minted separately when the requester activates it.
        await _accessRequestRepository.ResolveWithDecisionAsync(request, decision, action, now);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

        // The request just left the pending queue; tell every approver of this collection to re-fetch.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(request.CollectionId);

        // Tell the requester their request was resolved, so their "My requests" view flips to approved/denied and
        // an approval becomes activatable without a manual refresh.
        await _requesterNotifier.NotifyRequesterAsync(request.RequesterId);

        // The repository stamped Action/ActionDate in the guarded UPDATE; bring the entity to match before projecting
        // rather than re-reading.
        request.Action = action;
        request.ActionDate = now;
        var details = AccessRequestDetails.From(request, now);
        details.Decisions =
        [
            new AccessRequestDecision
            {
                DeciderKind = AccessDeciderKind.Human,
                ApproverId = userId,
                Comment = decision.Comment,
                Verdict = decision.Verdict,
                DecidedAt = now,
            },
        ];
        return details;
    }
}

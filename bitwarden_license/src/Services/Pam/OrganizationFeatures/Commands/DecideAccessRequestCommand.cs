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
    private readonly IRequesterMailNotifier _requesterMailNotifier;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public DecideAccessRequestCommand(
        IAccessRequestRepository accessRequestRepository,
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IApproverInboxNotifier approverInboxNotifier,
        IRequesterNotifier requesterNotifier,
        IRequesterMailNotifier requesterMailNotifier,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _accessRequestRepository = accessRequestRepository;
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _approverInboxNotifier = approverInboxNotifier;
        _requesterNotifier = requesterNotifier;
        _requesterMailNotifier = requesterMailNotifier;
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

        // An extension is decided when it is created: RequestLeaseExtensionCommand writes it already Approved with its
        // automatic verdict and pushes the parent lease's end out in place. No approver route reaches one today -- the
        // Pending check below already refuses it -- so this guard is a backstop, and a deliberate one. The spec models
        // human-approved extensions (ExtensionApprovedExtendsParentLease fires for both kinds, and
        // ExtensionDeniedParentGone exists only for the human case), and the day one is routed here it must extend the
        // parent in place rather than resolve into an activatable approval. Failing loudly now means that work cannot
        // silently reopen the second-lease hole this ordering closes.
        if (request.ExtensionOfLeaseId is not null)
        {
            throw new BadRequestException("An extension is approved when it is requested and cannot be decided.");
        }

        if (request.Action != AccessRequestAction.None)
        {
            throw new ConflictException("This request has already been resolved.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Once the window has lapsed the clock has closed the request: it is derived Expired everywhere it is read,
        // and neither verdict may restamp it -- an approval would mint a dead "approved" state, and a denial would
        // rewrite a row users already saw as Expired. 409 like already-resolved, because the clock resolved it.
        // (This deliberately retires the earlier "denial is still allowed to close out the audit trail" behavior.)
        if (!request.IsWindowOpen(now))
        {
            throw new ConflictException("This request's window has already ended.");
        }

        // Self-approval is blocked server-side even though the client disables the buttons. Surfaced as 400 rather
        // than 403 because Bitwarden clients treat 403 as a forced logout.
        if (request.RequesterId == userId)
        {
            throw new BadRequestException("You cannot decide your own request.");
        }

        var approved = submission.Verdict == AccessDecisionVerdict.Approve;

        // A denial must say why: the reason is what the requester's "denied" notification carries and what the audit
        // record explains the refusal with, and once the request is resolved there is no second chance to supply it.
        // Whitespace is refused alongside null for the same reason the comment is nulled below. Enforced here and
        // not only by the client's disabled confirm button, because every caller writes to the same audit trail.
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

        // audit (before/after): the verdict is known up front, so both phases carry the resulting kind (approved or
        // denied). Record the attempt, then the outcome around the point of no return.
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

        // Approval records the verdict only. The lease that actually authorizes access is minted when the requester
        // activates the approved request (ActivateAccessRequestCommand) — until then they hold a startable approval,
        // not access. The automatic path still mints instantly at submit, where the requester is present and asking.
        await _accessRequestRepository.ResolveWithDecisionAsync(request, decision, action, now);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

        // The request just left the pending queue; tell every approver of this collection to re-fetch.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(request.CollectionId);

        // Tell the requester their request was resolved, so their "My requests" view flips to approved/denied and
        // an approval becomes activatable without a manual refresh.
        await _requesterNotifier.NotifyRequesterAsync(request.RequesterId);

        // The same news out of band, to the requester alone: the push only lands on a client that is already open,
        // and the approver is the actor here rather than an audience.
        await _requesterMailNotifier.NotifyDecisionAsync(request, approved);

        // The client repaints the row from Status, ResolvedAt, and the single Decisions element (verdict + comment),
        // so those must be accurate; the approver's denormalized name/email is resolved on the next read. Project
        // from what we just wrote rather than re-reading: the repository stamped Action/ActionDate in the guarded
        // UPDATE, so the entity is brought to match before projecting. No lease exists yet, the extension guard
        // above excluded extensions, and the window guard proved it open, so the derived status lands on Approved
        // or Denied.
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

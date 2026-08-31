using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class SubmitAccessRequestCommand : ISubmitAccessRequestCommand
{
    /// <summary>
    /// The global maximum lease window length, applied to both the automatic duration and the human-requested window
    /// when the governing rule sets no narrower cap of its own. A rule's <c>MaxLeaseDurationSeconds</c> only narrows
    /// this — see <see cref="LeaseDurationBounds"/>, which folds the two together.
    /// </summary>
    public const int MaxDurationSeconds = LeaseDurationBounds.GlobalMaxSeconds;

    private readonly ICipherRepository _cipherRepository;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessRuleEngine _ruleEngine;
    private readonly ICurrentContext _currentContext;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly IApproverMailNotifier _approverMailNotifier;
    private readonly IRequesterNotifier _requesterNotifier;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public SubmitAccessRequestCommand(
        ICipherRepository cipherRepository,
        IGoverningRuleResolver resolver,
        IAccessRuleEngine ruleEngine,
        ICurrentContext currentContext,
        IAccessLeaseRepository accessLeaseRepository,
        IAccessRequestRepository accessRequestRepository,
        IApproverInboxNotifier approverInboxNotifier,
        IApproverMailNotifier approverMailNotifier,
        IRequesterNotifier requesterNotifier,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _cipherRepository = cipherRepository;
        _resolver = resolver;
        _ruleEngine = ruleEngine;
        _currentContext = currentContext;
        _accessLeaseRepository = accessLeaseRepository;
        _accessRequestRepository = accessRequestRepository;
        _approverInboxNotifier = approverInboxNotifier;
        _approverMailNotifier = approverMailNotifier;
        _requesterNotifier = requesterNotifier;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task<AccessRequestResult> SubmitAsync(Guid userId, Guid cipherId, AccessRequestSubmission submission)
    {
        var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
        if (cipher is null)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));

        var governingRule = await _resolver.ResolveAsync(userId, cipherId, signals);
        if (governingRule is null)
        {
            throw new BadRequestException("This item does not require a lease.");
        }

        if (await _accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now) is not null)
        {
            throw new BadRequestException("You already have active access to this item.");
        }

        // Lapsed unanswered requests don't match here (derived Expired), so they correctly don't block a fresh
        // request.
        if (await _accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId, now) is not null)
        {
            throw new BadRequestException("You already have a pending request for this item.");
        }

        // An approved-but-not-yet-activated request already grants startable access; a second request would let the
        // caller stack grants. Lapsed approvals don't match here, so they correctly don't block a fresh request.
        if (await _accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(userId, cipherId, now) is not null)
        {
            throw new BadRequestException("You already have an approved request for this item.");
        }

        return governingRule.RequiresHumanApproval
            ? await RequestHumanApprovalAsync(userId, cipherId, governingRule, submission)
            : await ApproveAutomaticallyAsync(userId, cipherId, governingRule, submission, now, signals);
    }

    private async Task<AccessRequestResult> ApproveAutomaticallyAsync(
        Guid userId, Guid cipherId, GoverningRule governingRule, AccessRequestSubmission submission, DateTime now,
        AccessSignals signals)
    {
        if (submission.Start.HasValue || submission.End.HasValue)
        {
            throw new BadRequestException("This item is approved automatically; provide a duration, not a window.");
        }

        if (submission.DurationSeconds is not { } durationSeconds || durationSeconds <= 0)
        {
            throw new BadRequestException("A positive duration is required.");
        }

        // The governing rule's own cap, narrowed by the global ceiling. Enforced here rather than at activation because
        // activation mints exactly the window pinned at submit, so this is the only gate the duration passes through.
        var maxDurationSeconds = LeaseDurationBounds.EffectiveMax(governingRule.MaxLeaseDurationSeconds);
        if (durationSeconds > maxDurationSeconds)
        {
            throw new BadRequestException($"The requested duration exceeds the maximum of {maxDurationSeconds} seconds.");
        }

        // The cipher must satisfy its access rule's conditions (source IP, time of day, ...) before the request is
        // auto-approved. The resolver only routes a rule here when it carries no human-approval gate, so the engine
        // never asks for approval on this path; any non-allow outcome is a denial we surface to the caller.
        var evaluation = _ruleEngine.Evaluate(governingRule.Conditions, signals);
        if (evaluation.Outcome != AccessEvaluationOutcome.Allow)
        {
            throw new BadRequestException(AccessDenialMessage.For(evaluation));
        }

        var notAfter = now.AddSeconds(durationSeconds);

        var request = new AccessRequest
        {
            OrganizationId = governingRule.OrganizationId,
            CollectionId = governingRule.CollectionId,
            CipherId = cipherId,
            RequesterId = userId,
            RuleId = governingRule.RuleId,
            NotBefore = now,
            NotAfter = notAfter,
            Reason = string.IsNullOrWhiteSpace(submission.Reason) ? null : submission.Reason,
            Action = AccessRequestAction.Approved,
            CreationDate = now,
            ActionDate = now,
        };
        request.SetNewId();

        var decision = new AccessDecision
        {
            AccessRequestId = request.Id,
            DeciderKind = AccessDeciderKind.Automatic,
            Verdict = AccessDecisionVerdict.Approve,
            CreationDate = now,
        };
        decision.SetNewId();

        // audit (before/after): the request is auto-approved in one write, so the outcome is two events -- the
        // submission and the automatic approval (no human actor). Record the attempt, then both outcomes around the
        // point of no return.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RequestSubmitted,
            OccurredAt = now,
            OrganizationId = governingRule.OrganizationId,
            ActorId = userId,
            RequesterId = userId,
            CollectionId = governingRule.CollectionId,
            CipherId = cipherId,
            AccessRequestId = request.Id,
            Detail = request.Reason,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // Auto-approval records only the request and its automatic verdict — no lease. The requester explicitly
        // activates the approved request (ActivateAccessRequestCommand) to start the lease, exactly like the human
        // path after approval. Deferring the mint means the per-cipher single-active-lease guard runs at activation,
        // the one place a lease is now minted, rather than here.
        await _accessRequestRepository.CreateAutoApprovedAsync(request, decision);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
        // The automatic approval is a distinct event from the submission (it has no attempt of its own), so it gets
        // its own correlation id rather than sharing the submit pair's.
        await _accessAuditEventEmitter.EmitAsync(
            audit with
            {
                Kind = AccessAuditEventKind.RequestApproved,
                Phase = AccessAuditEventPhase.Outcome,
                ActorId = null,
                CorrelationId = Guid.NewGuid(),
            });

        // Tell the requester's other devices a new approved request exists, so "My requests" can offer to activate it
        // without a manual refresh.
        await _requesterNotifier.NotifyRequesterAsync(userId);

        return AccessRequestResult.Automatic(request, decision);
    }

    private async Task<AccessRequestResult> RequestHumanApprovalAsync(
        Guid userId, Guid cipherId, GoverningRule governingRule, AccessRequestSubmission submission)
    {
        if (submission.DurationSeconds.HasValue)
        {
            throw new BadRequestException("This item requires human approval; provide a start and end date, not a duration.");
        }

        if (string.IsNullOrWhiteSpace(submission.Reason))
        {
            throw new BadRequestException("A reason is required for items that need human approval.");
        }

        if (submission.Start is not { } start || submission.End is not { } end)
        {
            throw new BadRequestException("A start and end date are required.");
        }

        if (start >= end)
        {
            throw new BadRequestException("The start date must be before the end date.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // A window that has already closed can never produce access: the row would be born derived-Expired --
        // filtered out of the approver's pending inbox by its clock predicate, and refused by both Decide and
        // Cancel -- so it is refused here, where the requester can fix the dates. This guard is also what lets the
        // submit response derive against an open window by construction (AccessRequestResultResponseModel).
        if (end <= now)
        {
            throw new BadRequestException("The end date must be in the future.");
        }

        // Same per-rule cap as the automatic path: an approver can only act on the window pinned here, so a window that
        // exceeds the rule's maximum has to be refused at submit rather than left for the approver to notice.
        var maxDurationSeconds = LeaseDurationBounds.EffectiveMax(governingRule.MaxLeaseDurationSeconds);
        if ((end - start).TotalSeconds > maxDurationSeconds)
        {
            throw new BadRequestException($"The requested window exceeds the maximum of {maxDurationSeconds} seconds.");
        }

        var request = new AccessRequest
        {
            OrganizationId = governingRule.OrganizationId,
            CollectionId = governingRule.CollectionId,
            CipherId = cipherId,
            RequesterId = userId,
            RuleId = governingRule.RuleId,
            NotBefore = start,
            NotAfter = end,
            Reason = submission.Reason,
            // No Action is set: the request is born open, awaiting the approver.
            CreationDate = now,
        };

        // audit (before/after): record the submission attempt (the request has no id until it is created), then the
        // outcome carrying the new request id, around the point of no return.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RequestSubmitted,
            OccurredAt = now,
            OrganizationId = governingRule.OrganizationId,
            ActorId = userId,
            RequesterId = userId,
            CollectionId = governingRule.CollectionId,
            CipherId = cipherId,
            Detail = request.Reason,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        var created = await _accessRequestRepository.CreateAsync(request);

        await _accessAuditEventEmitter.EmitAsync(
            audit with { Phase = AccessAuditEventPhase.Outcome, AccessRequestId = created.Id });

        // A new request just entered the pending queue; tell every approver of this collection to re-fetch.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(created.CollectionId);

        // Alongside the push, not instead of it: the push only reaches an approver whose client is already open.
        await _approverMailNotifier.NotifyPendingRequestAsync(created);

        // Tell the requester's other devices a new pending request exists, so "My requests" reflects it without a
        // manual refresh.
        await _requesterNotifier.NotifyRequesterAsync(userId);

        return AccessRequestResult.Human(created);
    }
}

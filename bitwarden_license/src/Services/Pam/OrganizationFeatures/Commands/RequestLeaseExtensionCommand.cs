using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;
using Bit.Services.Pam.Utilities;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class RequestLeaseExtensionCommand : IRequestLeaseExtensionCommand
{
    /// <summary>
    /// Recorded on the automatic Deny decision if the parent lease ended before the extension could apply.
    /// Stored, not translated, so it has to mean one thing to whoever reads the request later.
    /// </summary>
    private const string LeaseEndedDenialComment = "The lease being extended has ended";

    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessRuleEngine _ruleEngine;
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly IRequesterNotifier _requesterNotifier;
    private readonly ICurrentContext _currentContext;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public RequestLeaseExtensionCommand(
        IAccessLeaseRepository accessLeaseRepository,
        IGoverningRuleResolver resolver,
        IAccessRuleEngine ruleEngine,
        IAccessRequestRepository accessRequestRepository,
        IApproverInboxNotifier approverInboxNotifier,
        IRequesterNotifier requesterNotifier,
        ICurrentContext currentContext,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _accessLeaseRepository = accessLeaseRepository;
        _resolver = resolver;
        _ruleEngine = ruleEngine;
        _accessRequestRepository = accessRequestRepository;
        _approverInboxNotifier = approverInboxNotifier;
        _requesterNotifier = requesterNotifier;
        _currentContext = currentContext;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task<AccessRequestDetails> ExtendAsync(Guid userId, AccessLeaseExtensionSubmission submission)
    {
        var lease = await _accessLeaseRepository.GetByIdAsync(submission.LeaseId);

        // 404 for both missing and someone else's lease; the caller can't probe for leases they don't own.
        if (lease is null || lease.RequesterId != userId)
        {
            throw new NotFoundException();
        }

        // An extension buys new access, so it needs the license the original lease was taken under. The
        // lease already running is untouched.
        _currentContext.RequireLicense(lease.OrganizationId);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Liveness is decided under the per-lease lock in CreateApprovedExtensionAsync; an ended lease yields a
        // denied request.

        // Extensions are judged against the rule the lease was granted under (falling back to the current rule when
        // none was recorded) and are auto-approved, subject to the rule's extension settings and automated conditions.
        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));
        var originatingRequest = await _accessRequestRepository.GetByIdAsync(lease.AccessRequestId);
        var pinnedRuleId = originatingRequest?.RuleId;
        var governingRule = pinnedRuleId is { } ruleId
            ? await _resolver.ResolvePinnedAsync(ruleId, lease.CollectionId)
            : await _resolver.ResolveAsync(userId, lease.CipherId, signals);
        if (governingRule is null)
        {
            throw new BadRequestException(pinnedRuleId is null
                ? "This item does not require a lease."
                : "The rule this lease was granted under is no longer active.");
        }

        if (!governingRule.AllowsExtensions)
        {
            throw new BadRequestException("This item does not allow extending a lease.");
        }

        if (submission.DurationSeconds <= 0)
        {
            throw new BadRequestException("A positive duration is required.");
        }

        // The rule's max extension length is the cap. A missing cap is treated as zero so a misconfigured
        // rule denies.
        if (submission.DurationSeconds > (governingRule.MaxExtensionDurationSeconds ?? 0))
        {
            throw new BadRequestException("The requested duration exceeds the maximum extension length for this item.");
        }

        if (string.IsNullOrWhiteSpace(submission.Reason))
        {
            throw new BadRequestException("A justification is required to extend a lease.");
        }

        // Automated conditions (e.g. an IP allowlist) must still hold at extension, as they do at activation.
        var denial = FindConditionDenial(governingRule, signals);
        if (denial is not null)
        {
            throw new BadRequestException(AccessDenialMessage.For(denial));
        }

        // A lease may be extended once. Friendly early check; the mint proc re-counts under a per-lease lock
        // and is the race-safe authority.
        if (await _accessRequestRepository.CountExtensionsByLeaseIdAsync(lease.Id) >= 1)
        {
            throw new BadRequestException("This lease has already been extended.");
        }

        // The extension window spans from the lease's current end to its new end; NotAfter is the lease's new end.
        var request = new AccessRequest
        {
            ExtensionOfLeaseId = lease.Id,
            OrganizationId = lease.OrganizationId,
            CollectionId = lease.CollectionId,
            CipherId = lease.CipherId,
            RequesterId = userId,
            RuleId = governingRule.RuleId,
            NotBefore = lease.NotAfter,
            NotAfter = lease.NotAfter.AddSeconds(submission.DurationSeconds),
            Reason = submission.Reason,
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

        // audit (before/after): records the attempt, then the outcome around the point of no return. Only
        // AlreadyExtended throws with nothing persisted, leaving the attempt with no outcome.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.LeaseExtended,
            OccurredAt = now,
            OrganizationId = lease.OrganizationId,
            ActorId = userId,
            RequesterId = lease.RequesterId,
            CollectionId = lease.CollectionId,
            CipherId = lease.CipherId,
            AccessRequestId = request.Id,
            AccessLeaseId = lease.Id,
            LeaseNotAfter = request.NotAfter,
            Detail = request.Reason,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        var outcome = await _accessRequestRepository.CreateApprovedExtensionAsync(
            request, decision, now, LeaseEndedDenialComment);

        if (outcome == AccessLeaseExtendOutcome.AlreadyExtended)
        {
            throw new BadRequestException("This lease has already been extended.");
        }

        if (outcome == AccessLeaseExtendOutcome.LeaseNotActive)
        {
            // The lease ran out or was ended under the request. The repository recorded that as a denied request
            // rather than refusing the write, so this is a resolved outcome to report, not an error to throw.
            await _accessAuditEventEmitter.EmitAsync(
                audit with
                {
                    Kind = AccessAuditEventKind.RequestDenied,
                    Phase = AccessAuditEventPhase.Outcome,
                    LeaseNotAfter = lease.NotAfter,
                    Detail = LeaseEndedDenialComment,
                });

            // Only the requester's own devices need this: nothing about the collection's leases changed, so the
            // approver inbox has nothing to re-fetch.
            await _requesterNotifier.NotifyRequesterAsync(lease.RequesterId);

            return Project(request, AccessRequestAction.Denied, AccessDecisionVerdict.Deny,
                LeaseEndedDenialComment, now);
        }

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

        // The parent lease's window just grew: notify the collection's approvers and the requester's other devices.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(lease.CollectionId);
        await _requesterNotifier.NotifyRequesterAsync(lease.RequesterId);

        // The parent lease's end has already been pushed out, so the next access-state snapshot re-emits the longer
        // countdown.
        return Project(request, AccessRequestAction.Approved, AccessDecisionVerdict.Approve, comment: null, now);
    }

    /// <summary>
    /// Evaluates the governing rule's automated conditions against the caller's signals, with the approval gate
    /// stripped.
    /// </summary>
    private AccessEvaluation? FindConditionDenial(GoverningRule governingRule, AccessSignals signals)
    {
        if (governingRule.ConditionsUnreadable)
        {
            return AccessEvaluation.Deny(DenyReason.UnsupportedCondition);
        }

        var evaluation = _ruleEngine.Evaluate(governingRule.AutomatedConditions, signals);
        return evaluation.Outcome switch
        {
            AccessEvaluationOutcome.Allow => null,
            AccessEvaluationOutcome.RequiresApproval => AccessEvaluation.Deny(DenyReason.UnsupportedCondition),
            _ => evaluation,
        };
    }

    /// <summary>
    /// Projects the extension state the client renders from what was just written. <paramref name="action"/> and
    /// <paramref name="verdict"/> come from the repository's outcome rather than <paramref name="request"/>, since a
    /// lease that ended under the request is written Denied.
    /// </summary>
    private static AccessRequestDetails Project(AccessRequest request, AccessRequestAction action,
        AccessDecisionVerdict verdict, string? comment, DateTime now)
    {
        request.Action = action;
        var details = AccessRequestDetails.From(request, now);
        details.Decisions =
        [
            new AccessRequestDecision
            {
                DeciderKind = AccessDeciderKind.Automatic,
                Verdict = verdict,
                Comment = comment,
                DecidedAt = now,
            }
        ];
        return details;
    }
}

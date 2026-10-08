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
    /// <summary>The automatic Deny decision's comment when the lease ended before the extension could apply.</summary>
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

        // An extension buys new access, so it needs a license; the running lease is unaffected.
        _currentContext.RequireLicense(lease.OrganizationId);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Liveness is decided under the per-lease lock in CreateApprovedExtensionAsync; an ended lease yields a
        // denied request.

        // Judged against the rule pinned on the lease's request, or the current rule when none is pinned.
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

        // A missing cap counts as zero, so a misconfigured rule denies.
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

        // A lease may be extended once. An early check; the extension proc re-counts under the per-lease lock.
        if (await _accessRequestRepository.CountExtensionsByLeaseIdAsync(lease.Id) >= 1)
        {
            throw new BadRequestException("This lease has already been extended.");
        }

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

        // Attempt before the write, outcome after. AlreadyExtended leaves the attempt without one.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.LeaseExtended,
            OccurredDate = now,
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
            // The lease ended first. The repository recorded a denied request, which is reported, not thrown.
            await _accessAuditEventEmitter.EmitAsync(
                audit with
                {
                    Kind = AccessAuditEventKind.RequestDenied,
                    Phase = AccessAuditEventPhase.Outcome,
                    LeaseNotAfter = lease.NotAfter,
                    Detail = LeaseEndedDenialComment,
                });

            await _requesterNotifier.NotifyRequesterAsync(lease.RequesterId);

            return Project(request, AccessRequestAction.Denied, AccessDecisionVerdict.Deny,
                LeaseEndedDenialComment, now);
        }

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

        await _approverInboxNotifier.NotifyCollectionApproversAsync(lease.CollectionId);
        await _requesterNotifier.NotifyRequesterAsync(lease.RequesterId);

        // The lease's end is already pushed out, so the next access-state read shows the longer countdown.
        return Project(request, AccessRequestAction.Approved, AccessDecisionVerdict.Approve, comment: null, now);
    }

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
    /// Projects what was just written. <paramref name="action"/> and <paramref name="verdict"/> come from the
    /// repository's outcome, since a lease that ended first is written Denied.
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

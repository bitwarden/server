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

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class RequestLeaseExtensionCommand : IRequestLeaseExtensionCommand
{
    /// <summary>
    /// Recorded on the automatic Deny decision when the parent lease ended before the extension could apply. Stored,
    /// not translated: it is the reason the denial happened, and it has to mean one thing to whoever reads the
    /// request later. The client renders its own copy from the request's status.
    /// </summary>
    private const string LeaseEndedDenialComment = "The lease being extended has ended";

    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly IRequesterNotifier _requesterNotifier;
    private readonly ICurrentContext _currentContext;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public RequestLeaseExtensionCommand(
        IAccessLeaseRepository accessLeaseRepository,
        IGoverningRuleResolver resolver,
        IAccessRequestRepository accessRequestRepository,
        IApproverInboxNotifier approverInboxNotifier,
        IRequesterNotifier requesterNotifier,
        ICurrentContext currentContext,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _accessLeaseRepository = accessLeaseRepository;
        _resolver = resolver;
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

        // 404 for both missing and someone else's lease, so the caller can't probe for leases they don't own.
        if (lease is null || lease.RequesterId != userId)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // No pre-check that the lease is still live. That question is settled once, under the per-lease lock in
        // CreateApprovedExtensionAsync, which answers it by recording a denied request rather than by refusing the
        // call — so the requester whose lease ran out while the Extend dialog was open gets something to inspect
        // (PM-42632). A pre-check here would only reproduce that verdict as a 409 in the common case and leave the
        // raced case behaving differently.

        // Extensions reuse the cipher's governing rule, but never its approval gate: they are always auto-approved,
        // gated only by the rule opting in and the per-lease maximum.
        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));
        var governingRule = await _resolver.ResolveAsync(userId, lease.CipherId, signals);
        if (governingRule is null)
        {
            throw new BadRequestException("This item does not require a lease.");
        }

        if (!governingRule.AllowsExtensions)
        {
            throw new BadRequestException("This item does not allow extending a lease.");
        }

        if (submission.DurationSeconds <= 0)
        {
            throw new BadRequestException("A positive duration is required.");
        }

        // The rule's max extension length is the cap (the admin picks it from presets); it is always set when
        // AllowsExtensions is true. A missing cap is treated as zero so a misconfigured rule denies.
        if (submission.DurationSeconds > (governingRule.MaxExtensionDurationSeconds ?? 0))
        {
            throw new BadRequestException("The requested duration exceeds the maximum extension length for this item.");
        }

        if (string.IsNullOrWhiteSpace(submission.Reason))
        {
            throw new BadRequestException("A justification is required to extend a lease.");
        }

        // A lease may be extended exactly once. Friendly early check; the mint proc re-counts under a per-lease lock
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

        // audit (before/after): record the extension attempt, then the outcome around the point of no return. A
        // refused extension still records an outcome when the refusal itself was written (the denial below); only
        // AlreadyExtended throws with nothing persisted, leaving the attempt as an in-doubt entry with no outcome.
        // AccessLeaseId is the parent lease; LeaseNotAfter is its new end.
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
            // The lease ran out or was ended under the request — typically while the Extend dialog sat open. The
            // repository recorded that as a denied request rather than refusing the write, so this is a resolved
            // outcome to report, not an error to throw (PM-42632). The pair's kind flips to RequestDenied, mirroring
            // how a refused activation reports LeaseActivationRejected against a LeaseActivated attempt.
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

        // The parent lease's window just grew. Tell every approver of the collection to re-fetch (their active-leases
        // and history views show the new end), and tell the requester's other devices so the banner/badge countdown
        // reflects the longer window without a manual refresh.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(lease.CollectionId);
        await _requesterNotifier.NotifyRequesterAsync(lease.RequesterId);

        // The parent lease's end has already been pushed out, so the next access-state snapshot re-emits the longer
        // countdown.
        return Project(request, AccessRequestAction.Approved, AccessDecisionVerdict.Approve, comment: null, now);
    }

    /// <summary>
    /// Projects the extension state the client renders from what was just written: <c>ExtensionOfLeaseId</c> set, plus
    /// the automatic decision that resolved it. <paramref name="action"/> and <paramref name="verdict"/> come from the
    /// repository's outcome rather than from <paramref name="request"/>, which carries the approved shape the caller
    /// asked for; a lease that ended under the request is written Denied, and only the comment says why. The entity is
    /// brought to match what was written before it is projected, so the derived status (Approved by the applied-
    /// extension carve-out, or terminal Denied) matches every later read of this row.
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

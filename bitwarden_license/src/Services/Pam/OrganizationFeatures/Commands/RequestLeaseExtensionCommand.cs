using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Context;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Errors;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class RequestLeaseExtensionCommand : IRequestLeaseExtensionCommand
{
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

    public async Task<CommandResult<AccessRequestDetails>> ExtendAsync(Guid userId, AccessLeaseExtensionSubmission submission)
    {
        var lease = await _accessLeaseRepository.GetByIdAsync(submission.LeaseId);

        // 404 for both missing and someone else's lease, so the caller can't probe for leases they don't own.
        if (lease is null || lease.RequesterId != userId)
        {
            return new AccessLeaseNotFound();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (lease.Status != AccessLeaseStatus.Active || lease.NotAfter <= now)
        {
            return new AccessLeaseNoLongerActive();
        }

        // Extensions reuse the cipher's governing rule, but never its approval gate: they are always auto-approved,
        // gated only by the rule opting in and the per-lease maximum.
        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));
        var governingRule = await _resolver.ResolveAsync(userId, lease.CipherId, signals);
        if (governingRule is null)
        {
            return new CipherNotGated();
        }

        if (!governingRule.AllowsExtensions)
        {
            return new ExtensionsNotAllowed();
        }

        if (submission.DurationSeconds <= 0)
        {
            return new DurationMustBePositive();
        }

        // The rule's max extension length is the cap (the admin picks it from presets); it is always set when
        // AllowsExtensions is true. A missing cap is treated as zero so a misconfigured rule denies.
        if (submission.DurationSeconds > (governingRule.MaxExtensionDurationSeconds ?? 0))
        {
            return new ExtensionExceedsMax();
        }

        if (string.IsNullOrWhiteSpace(submission.Reason))
        {
            return new ExtensionReasonRequired();
        }

        // A lease may be extended exactly once. Friendly early check; the mint proc re-counts under a per-lease lock
        // and is the race-safe authority.
        if (await _accessRequestRepository.CountExtensionsByLeaseIdAsync(lease.Id) >= 1)
        {
            return new AccessLeaseAlreadyExtended();
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
            Status = AccessRequestStatus.Approved,
            CreationDate = now,
            ResolvedDate = now,
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
        // failed extension (lease no longer active, or already extended) throws, leaving the attempt as an in-doubt
        // entry with no outcome. AccessLeaseId is the parent lease; LeaseNotAfter is its new end.
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

        var outcome = await _accessRequestRepository.CreateApprovedExtensionAsync(request, decision, now);

        switch (outcome)
        {
            case AccessLeaseExtendOutcome.LeaseNotActive:
                return new AccessLeaseNoLongerActive();
            case AccessLeaseExtendOutcome.AlreadyExtended:
                return new AccessLeaseAlreadyExtended();
        }

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

        // The parent lease's window just grew. Tell every approver of the collection to re-fetch (their active-leases
        // and history views show the new end), and tell the requester's other devices so the banner/badge countdown
        // reflects the longer window without a manual refresh.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(lease.CollectionId);
        await _requesterNotifier.NotifyRequesterAsync(lease.RequesterId);

        // Project the approved-extension state the client renders (Status approved + ExtensionOfLeaseId set) from
        // what we just wrote. The parent lease's end has already been pushed out, so the next access-state snapshot
        // re-emits the longer countdown.
        return new AccessRequestDetails
        {
            Id = request.Id,
            ExtensionOfLeaseId = request.ExtensionOfLeaseId,
            OrganizationId = request.OrganizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            RuleId = request.RuleId,
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            Reason = request.Reason,
            Status = AccessRequestStatus.Approved,
            CreationDate = request.CreationDate,
            ResolvedDate = request.ResolvedDate,
        };
    }
}

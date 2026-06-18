using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Engine;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Pam.Repositories;
using Bit.Pam.Services;

namespace Bit.Pam.OrganizationFeatures.Commands;

public class RequestLeaseExtensionCommand : IRequestLeaseExtensionCommand
{
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly IRequesterNotifier _requesterNotifier;
    private readonly ICurrentContext _currentContext;
    private readonly TimeProvider _timeProvider;

    public RequestLeaseExtensionCommand(
        IAccessLeaseRepository accessLeaseRepository,
        IGoverningRuleResolver resolver,
        IAccessRequestRepository accessRequestRepository,
        IApproverInboxNotifier approverInboxNotifier,
        IRequesterNotifier requesterNotifier,
        ICurrentContext currentContext,
        TimeProvider timeProvider)
    {
        _accessLeaseRepository = accessLeaseRepository;
        _resolver = resolver;
        _accessRequestRepository = accessRequestRepository;
        _approverInboxNotifier = approverInboxNotifier;
        _requesterNotifier = requesterNotifier;
        _currentContext = currentContext;
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

        if (lease.Status != AccessLeaseStatus.Active || lease.NotAfter <= now)
        {
            throw new ConflictException("This lease is no longer active.");
        }

        // Extensions reuse the cipher's governing rule, but never its approval gate: they are always auto-approved,
        // gated only by the rule opting in and the per-lease maximum.
        var signals = AccessSignals.From(_currentContext, new DateTimeOffset(now, TimeSpan.Zero));
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

        var outcome = await _accessRequestRepository.CreateApprovedExtensionAsync(request, decision, now);

        switch (outcome)
        {
            case AccessLeaseExtendOutcome.LeaseNotActive:
                throw new ConflictException("This lease is no longer active.");
            case AccessLeaseExtendOutcome.AlreadyExtended:
                throw new BadRequestException("This lease has already been extended.");
        }

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
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            Reason = request.Reason,
            Status = AccessRequestStatus.Approved,
            CreationDate = request.CreationDate,
            ResolvedDate = request.ResolvedDate,
        };
    }
}

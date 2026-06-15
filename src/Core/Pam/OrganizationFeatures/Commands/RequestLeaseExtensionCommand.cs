using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;

namespace Bit.Core.Pam.OrganizationFeatures.Commands;

public class RequestLeaseExtensionCommand : IRequestLeaseExtensionCommand
{
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly TimeProvider _timeProvider;

    public RequestLeaseExtensionCommand(
        IAccessLeaseRepository accessLeaseRepository,
        IGoverningRuleResolver resolver,
        IAccessRequestRepository accessRequestRepository,
        TimeProvider timeProvider)
    {
        _accessLeaseRepository = accessLeaseRepository;
        _resolver = resolver;
        _accessRequestRepository = accessRequestRepository;
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
        var governingRule = await _resolver.ResolveAsync(userId, lease.CipherId);
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

        if (submission.DurationSeconds > SubmitAccessRequestCommand.MaxDurationSeconds)
        {
            throw new BadRequestException(
                $"The requested duration exceeds the maximum of {SubmitAccessRequestCommand.MaxDurationSeconds} seconds.");
        }

        if (string.IsNullOrWhiteSpace(submission.Reason))
        {
            throw new BadRequestException("A justification is required to extend a lease.");
        }

        // MaxExtensions is guaranteed positive when AllowsExtensions is true (enforced on rule create/update); a
        // missing cap is treated as zero so a misconfigured rule denies rather than grants unbounded extensions.
        var maxExtensions = governingRule.MaxExtensions ?? 0;

        // Friendly early check; the mint proc re-counts under a per-lease lock and is the race-safe authority.
        var priorExtensions = await _accessRequestRepository.CountExtensionsByLeaseIdAsync(lease.Id);
        if (priorExtensions >= maxExtensions)
        {
            throw new BadRequestException("This lease has reached the maximum number of extensions.");
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

        var outcome = await _accessRequestRepository.CreateApprovedExtensionAsync(request, decision, maxExtensions, now);

        switch (outcome)
        {
            case AccessLeaseExtendOutcome.LeaseNotActive:
                throw new ConflictException("This lease is no longer active.");
            case AccessLeaseExtendOutcome.MaxExtensionsReached:
                throw new BadRequestException("This lease has reached the maximum number of extensions.");
        }

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

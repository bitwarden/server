using System.Net;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Pam.Engine;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Pam.OrganizationFeatures.Commands;

public class SubmitAccessRequestCommand : ISubmitAccessRequestCommand
{
    /// <summary>
    /// The maximum lease window length, applied to both the automatic duration and the human-requested window.
    /// Global for v0; per-rule configuration is a later concern.
    /// </summary>
    public const int MaxDurationSeconds = 24 * 60 * 60;

    private readonly ICipherRepository _cipherRepository;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessRuleEngine _ruleEngine;
    private readonly ICurrentContext _currentContext;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly TimeProvider _timeProvider;

    public SubmitAccessRequestCommand(
        ICipherRepository cipherRepository,
        IGoverningRuleResolver resolver,
        IAccessRuleEngine ruleEngine,
        ICurrentContext currentContext,
        IAccessLeaseRepository accessLeaseRepository,
        IAccessRequestRepository accessRequestRepository,
        IApproverInboxNotifier approverInboxNotifier,
        TimeProvider timeProvider)
    {
        _cipherRepository = cipherRepository;
        _resolver = resolver;
        _ruleEngine = ruleEngine;
        _currentContext = currentContext;
        _accessLeaseRepository = accessLeaseRepository;
        _accessRequestRepository = accessRequestRepository;
        _approverInboxNotifier = approverInboxNotifier;
        _timeProvider = timeProvider;
    }

    public async Task<AccessRequestResult> SubmitAsync(Guid userId, Guid cipherId, AccessRequestSubmission submission)
    {
        var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
        if (cipher is null)
        {
            throw new NotFoundException();
        }

        var governingRule = await _resolver.ResolveAsync(userId, cipherId);
        if (governingRule is null)
        {
            throw new BadRequestException("This item does not require a lease.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (await _accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now) is not null)
        {
            throw new BadRequestException("You already have active access to this item.");
        }

        if (await _accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId) is not null)
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
            : await IssueAutomaticLeaseAsync(userId, cipherId, governingRule, submission, now);
    }

    private async Task<AccessRequestResult> IssueAutomaticLeaseAsync(
        Guid userId, Guid cipherId, GoverningRule governingRule, AccessRequestSubmission submission, DateTime now)
    {
        if (submission.Start.HasValue || submission.End.HasValue)
        {
            throw new BadRequestException("This item is approved automatically; provide a duration, not a window.");
        }

        if (submission.DurationSeconds is not { } durationSeconds || durationSeconds <= 0)
        {
            throw new BadRequestException("A positive duration is required.");
        }

        if (durationSeconds > MaxDurationSeconds)
        {
            throw new BadRequestException($"The requested duration exceeds the maximum of {MaxDurationSeconds} seconds.");
        }

        // The cipher must satisfy its access rule's conditions (source IP, time of day, ...) before an automatic
        // lease is issued. The resolver only routes a rule here when it carries no human-approval gate, so the
        // engine never asks for approval on this path; any non-allow outcome is a denial we surface to the caller.
        var evaluation = _ruleEngine.Evaluate(governingRule.Condition, BuildSignals(now));
        if (evaluation.Outcome != AccessEvaluationOutcome.Allow)
        {
            throw new BadRequestException(DenyMessage(evaluation));
        }

        var notAfter = now.AddSeconds(durationSeconds);

        var request = new AccessRequest
        {
            OrganizationId = governingRule.OrganizationId,
            CollectionId = governingRule.CollectionId,
            CipherId = cipherId,
            RequesterId = userId,
            NotBefore = now,
            NotAfter = notAfter,
            Reason = string.IsNullOrWhiteSpace(submission.Reason) ? null : submission.Reason,
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

        var lease = new AccessLease
        {
            AccessRequestId = request.Id,
            OrganizationId = governingRule.OrganizationId,
            CollectionId = governingRule.CollectionId,
            CipherId = cipherId,
            RequesterId = userId,
            Status = AccessLeaseStatus.Active,
            NotBefore = now,
            NotAfter = notAfter,
            CreationDate = now,
        };
        lease.SetNewId();

        await _accessLeaseRepository.CreateAutoApprovedAsync(request, decision, lease, now);

        return AccessRequestResult.Automatic(lease);
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

        if ((end - start).TotalSeconds > MaxDurationSeconds)
        {
            throw new BadRequestException($"The requested window exceeds the maximum of {MaxDurationSeconds} seconds.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var request = new AccessRequest
        {
            OrganizationId = governingRule.OrganizationId,
            CollectionId = governingRule.CollectionId,
            CipherId = cipherId,
            RequesterId = userId,
            NotBefore = start,
            NotAfter = end,
            Reason = submission.Reason,
            Status = AccessRequestStatus.Pending,
            CreationDate = now,
        };

        var created = await _accessRequestRepository.CreateAsync(request);

        // A new request just entered the pending queue; tell every approver of this collection to re-fetch.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(created.CollectionId);

        return AccessRequestResult.Human(created);
    }

    private AccessSignals BuildSignals(DateTime now) => new()
    {
        IpAddress = IPAddress.TryParse(_currentContext.IpAddress, out var ip) ? ip : null,
        Timestamp = new DateTimeOffset(now, TimeSpan.Zero),
    };

    private static string DenyMessage(AccessEvaluation evaluation) => evaluation.Reason switch
    {
        DenyReason.NotWithinIpRange => "Access to this item is not permitted from your current network.",
        DenyReason.NotWithinTimeWindow => "Access to this item is not permitted at this time.",
        _ => "Access to this item is not permitted right now.",
    };
}

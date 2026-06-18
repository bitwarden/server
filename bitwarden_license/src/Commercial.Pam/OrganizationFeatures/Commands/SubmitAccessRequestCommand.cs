using Bit.Commercial.Pam.Engine;
using Bit.Commercial.Pam.Models;
using Bit.Commercial.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Commercial.Pam.Services;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Commercial.Pam.OrganizationFeatures.Commands;

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
    private readonly IRequesterNotifier _requesterNotifier;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMailService _mailService;
    private readonly ILogger<SubmitAccessRequestCommand> _logger;
    private readonly TimeProvider _timeProvider;

    public SubmitAccessRequestCommand(
        ICipherRepository cipherRepository,
        IGoverningRuleResolver resolver,
        IAccessRuleEngine ruleEngine,
        ICurrentContext currentContext,
        IAccessLeaseRepository accessLeaseRepository,
        IAccessRequestRepository accessRequestRepository,
        IApproverInboxNotifier approverInboxNotifier,
        IRequesterNotifier requesterNotifier,
        ICollectionRepository collectionRepository,
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IMailService mailService,
        ILogger<SubmitAccessRequestCommand> logger,
        TimeProvider timeProvider)
    {
        _cipherRepository = cipherRepository;
        _resolver = resolver;
        _ruleEngine = ruleEngine;
        _currentContext = currentContext;
        _accessLeaseRepository = accessLeaseRepository;
        _accessRequestRepository = accessRequestRepository;
        _approverInboxNotifier = approverInboxNotifier;
        _requesterNotifier = requesterNotifier;
        _collectionRepository = collectionRepository;
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _mailService = mailService;
        _logger = logger;
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

        if (durationSeconds > MaxDurationSeconds)
        {
            throw new BadRequestException($"The requested duration exceeds the maximum of {MaxDurationSeconds} seconds.");
        }

        // The cipher must satisfy its access rule's conditions (source IP, time of day, ...) before the request is
        // auto-approved. The resolver only routes a rule here when it carries no human-approval gate, so the engine
        // never asks for approval on this path; any non-allow outcome is a denial we surface to the caller.
        var evaluation = _ruleEngine.Evaluate(governingRule.Conditions, signals);
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

        // Auto-approval records only the request and its automatic verdict — no lease. The requester explicitly
        // activates the approved request (ActivateAccessRequestCommand) to start the lease, exactly like the human
        // path after approval. Deferring the mint means the per-cipher single-active-lease guard runs at activation,
        // the one place a lease is now minted, rather than here.
        await _accessRequestRepository.CreateAutoApprovedAsync(request, decision);

        // Tell the requester's other devices a new approved request exists, so "My requests" can offer to activate it
        // without a manual refresh.
        await _requesterNotifier.NotifyRequesterAsync(userId);

        return AccessRequestResult.Automatic(request);
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

        // Tell the requester's other devices a new pending request exists, so "My requests" reflects it without a
        // manual refresh.
        await _requesterNotifier.NotifyRequesterAsync(userId);

        // Also email the collection's approvers so they learn about the request outside of an open session.
        await NotifyApproversByEmailAsync(created);

        return AccessRequestResult.Human(created);
    }

    /// <summary>
    /// Emails the managers (approvers) of the request's collection that a new request is pending their review.
    /// Best-effort: failures are logged and swallowed so they never fail the request submission.
    /// </summary>
    private async Task NotifyApproversByEmailAsync(AccessRequest request)
    {
        try
        {
            var managerIds = await _collectionRepository.GetManagingUserIdsAsync(request.CollectionId);

            // The requester may manage the collection themselves; never notify them of their own request.
            var recipientIds = managerIds.Where(id => id != request.RequesterId).ToList();
            if (recipientIds.Count == 0)
            {
                return;
            }

            var managers = await _userRepository.GetManyAsync(recipientIds);
            var managerEmails = managers
                .Select(u => u.Email)
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (managerEmails.Count == 0)
            {
                return;
            }

            var requester = await _userRepository.GetByIdAsync(request.RequesterId);
            if (requester is null || string.IsNullOrWhiteSpace(requester.Email))
            {
                _logger.LogWarning(
                    "Skipping PAM approver email for access request {AccessRequestId}; requester not found or has no email.",
                    request.Id);
                return;
            }

            var organization = await _organizationRepository.GetByIdAsync(request.OrganizationId);
            if (organization is null)
            {
                _logger.LogWarning(
                    "Skipping PAM approver email for access request {AccessRequestId}; organization not found.",
                    request.Id);
                return;
            }

            await _mailService.SendPamPendingAccessRequestEmailsAsync(
                managerEmails,
                organization.Name,
                requester.Name,
                requester.Email,
                request.NotBefore,
                request.NotAfter,
                request.Reason);
        }
        catch (Exception ex)
        {
            // Best effort: the request is already persisted and the inbox push already fired. An email failure
            // must never fail the submission, so log and move on.
            _logger.LogError(ex,
                "Failed to send PAM approver emails for access request {AccessRequestId}.", request.Id);
        }
    }

    private static string DenyMessage(AccessEvaluation evaluation) => evaluation.Reason switch
    {
        DenyReason.NotWithinIpRange => "Access to this item is not permitted from your current network.",
        DenyReason.NotWithinTimeWindow => "Access to this item is not permitted at this time.",
        _ => "Access to this item is not permitted right now.",
    };
}

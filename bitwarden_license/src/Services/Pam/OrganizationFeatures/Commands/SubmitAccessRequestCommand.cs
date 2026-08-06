using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

/// <inheritdoc cref="ISubmitAccessRequestCommand" />
public class SubmitAccessRequestCommand : ISubmitAccessRequestCommand
{
    /// <summary>
    /// The maximum lease window length. Global for this cut; per-rule configuration is a later concern.
    /// </summary>
    public const int MaxDurationSeconds = 24 * 60 * 60;

    private readonly ICipherRepository _cipherRepository;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessRuleEngine _ruleEngine;
    private readonly ICurrentContext _currentContext;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly TimeProvider _timeProvider;

    public SubmitAccessRequestCommand(
        ICipherRepository cipherRepository,
        IGoverningRuleResolver resolver,
        IAccessRuleEngine ruleEngine,
        ICurrentContext currentContext,
        IAccessLeaseRepository accessLeaseRepository,
        IAccessRequestRepository accessRequestRepository,
        TimeProvider timeProvider)
    {
        _cipherRepository = cipherRepository;
        _resolver = resolver;
        _ruleEngine = ruleEngine;
        _currentContext = currentContext;
        _accessLeaseRepository = accessLeaseRepository;
        _accessRequestRepository = accessRequestRepository;
        _timeProvider = timeProvider;
    }

    public async Task<AccessRequest> SubmitAsync(Guid userId, Guid cipherId, int durationSeconds, string? reason)
    {
        var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
        if (cipher is null)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var signals = AccessSignals.From(_currentContext.IpAddress);

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

        // An approved-but-not-yet-activated request already grants startable access; a second would let the caller
        // stack grants. Lapsed approvals don't match here, so they correctly don't block a fresh request.
        if (await _accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(userId, cipherId, now) is not null)
        {
            throw new BadRequestException("You already have an approved request for this item.");
        }

        // This poc2 cut ships only the automatic request→activate flow; the human-approval path (approver inbox,
        // notifications, mail) is deferred to a later slice.
        if (governingRule.RequiresHumanApproval)
        {
            throw new BadRequestException("Human approval is not available in this build.");
        }

        if (durationSeconds <= 0)
        {
            throw new BadRequestException("A positive duration is required.");
        }

        if (durationSeconds > MaxDurationSeconds)
        {
            throw new BadRequestException($"The requested duration exceeds the maximum of {MaxDurationSeconds} seconds.");
        }

        // The cipher must still satisfy its rule's conditions (source IP, time of day, …) at submit. The resolver only
        // routes rules with no human-approval gate here, so any non-allow outcome is a denial we surface.
        var evaluation = _ruleEngine.Evaluate(governingRule.Conditions, signals);
        if (evaluation.Outcome != AccessEvaluationOutcome.Allow)
        {
            throw new BadRequestException("Access to this item is currently denied by its access rule.");
        }

        var request = new AccessRequest
        {
            OrganizationId = governingRule.OrganizationId,
            CollectionId = governingRule.CollectionId,
            CipherId = cipherId,
            RequesterId = userId,
            RuleId = governingRule.RuleId,
            NotBefore = now,
            NotAfter = now.AddSeconds(durationSeconds),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason,
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
        // activates the approved request to start the lease, so the single-active-lease guard runs at activation, the
        // one place a lease is minted.
        await _accessRequestRepository.CreateAutoApprovedAsync(request, decision);

        return request;
    }
}

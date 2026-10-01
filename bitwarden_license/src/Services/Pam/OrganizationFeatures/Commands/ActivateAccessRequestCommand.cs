using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;
using Bit.Services.Pam.Utilities;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class ActivateAccessRequestCommand : IActivateAccessRequestCommand
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly ISingleActiveLeaseEvaluator _singleActiveLeaseEvaluator;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessRuleEngine _ruleEngine;
    private readonly ICurrentContext _currentContext;

    public ActivateAccessRequestCommand(
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        ISingleActiveLeaseEvaluator singleActiveLeaseEvaluator,
        IGoverningRuleResolver resolver,
        IAccessRuleEngine ruleEngine,
        ICurrentContext currentContext)
    {
        _accessRequestRepository = accessRequestRepository;
        _accessLeaseRepository = accessLeaseRepository;
        _singleActiveLeaseEvaluator = singleActiveLeaseEvaluator;
        _resolver = resolver;
        _ruleEngine = ruleEngine;
        _currentContext = currentContext;
    }

    public async Task<AccessLease> ActivateAsync(Guid userId, Guid requestId, DateTime now)
    {
        var request = await _accessRequestRepository.GetByIdAsync(requestId);

        // 404 for both missing and requests belonging to another user.
        if (request is null || request.RequesterId != userId)
        {
            throw new NotFoundException();
        }

        // An extension never activates; it applied itself at approval by extending the parent lease's end.
        if (request.ExtensionOfLeaseId is not null)
        {
            throw new BadRequestException("This request extended an existing lease and cannot start a new one.");
        }

        // Idempotent while the produced lease is live; a revoked or lapsed lease is final.
        var existing = await _accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        if (existing is not null)
        {
            if (existing.IsLive(now))
            {
                return existing;
            }
            throw new ConflictException("This request's access has already been used and is no longer active.");
        }

        // After the idempotency guard: a live lease survives de-licensing, but minting a new one needs a license.
        _currentContext.RequireLicense(request.OrganizationId);

        if (request.Action != AccessRequestAction.Approved)
        {
            throw new ConflictException(request.Action == AccessRequestAction.None
                ? "This request has not been approved yet."
                : "This request can no longer be activated.");
        }

        if (request.NotBefore > now)
        {
            throw new BadRequestException("The approved access window has not started yet.");
        }

        if (!request.IsWindowOpen(now))
        {
            throw new BadRequestException("The approved access window has already ended.");
        }

        var lease = new AccessLease
        {
            AccessRequestId = request.Id,
            OrganizationId = request.OrganizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            // NotBefore is now, not backdated to the approved window's start; NotAfter stays the approved end.
            NotBefore = now,
            NotAfter = request.NotAfter,
            CreationDate = now,
        };
        lease.SetNewId();

        // Binds only where every cipher path is singleton-governed; enforced under a range lock in the mint proc.
        var enforceSingleActiveLease = await _singleActiveLeaseEvaluator.AppliesAsync(userId, request.CipherId);

        // Automated conditions (e.g. an IP allowlist) must still hold at activation, not just at submit.
        var denial = await FindConditionDenialAsync(userId, request, now);
        if (denial is not null)
        {
            throw new BadRequestException(AccessDenialMessage.For(denial));
        }

        var outcome = await _accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, enforceSingleActiveLease);

        if (outcome == AccessLeaseMintOutcome.SingleActiveLeaseConflict)
        {
            throw new ConflictException("Another active lease exists for this item. Try again once it ends.");
        }

        if (outcome == AccessLeaseMintOutcome.PreconditionFailed)
        {
            // Lost the race to another activation; a live winner lease still counts as success.
            var winner = await _accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
            if (winner?.IsLive(now) == true)
            {
                return winner;
            }
            throw new ConflictException("This request can no longer be activated.");
        }

        return lease;
    }

    /// <summary>
    /// Re-evaluates the governing rule's automated conditions against the caller's signals at activation time.
    /// </summary>
    /// <remarks>
    /// Uses the rule pinned on the request, not whichever rule governs the cipher today; the approval gate is stripped.
    /// </remarks>
    private async Task<AccessEvaluation?> FindConditionDenialAsync(Guid userId, AccessRequest request, DateTime now)
    {
        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));

        var governingRule = request.RuleId is { } ruleId
            ? await _resolver.ResolvePinnedAsync(ruleId, request.CollectionId)
            : await _resolver.ResolveAsync(userId, request.CipherId, signals);

        // No rule left to enforce: the cipher is no longer gated, so the approved request activates unconditionally.
        if (governingRule is null)
        {
            return null;
        }

        if (governingRule.ConditionsUnreadable)
        {
            return AccessEvaluation.Deny(DenyReason.UnsupportedCondition);
        }

        var evaluation = _ruleEngine.Evaluate(governingRule.AutomatedConditions, signals);
        return evaluation.Outcome switch
        {
            AccessEvaluationOutcome.Allow => null,
            // Already approved; there is no second approver to route to.
            AccessEvaluationOutcome.RequiresApproval => AccessEvaluation.Deny(DenyReason.UnsupportedCondition),
            _ => evaluation,
        };
    }
}

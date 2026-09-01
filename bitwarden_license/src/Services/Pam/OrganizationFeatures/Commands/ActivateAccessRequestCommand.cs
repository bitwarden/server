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

public class ActivateAccessRequestCommand : IActivateAccessRequestCommand
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IApproverInboxNotifier _approverInboxNotifier;
    private readonly IRequesterNotifier _requesterNotifier;
    private readonly ISingleActiveLeaseEvaluator _singleActiveLeaseEvaluator;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessRuleEngine _ruleEngine;
    private readonly ICurrentContext _currentContext;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;

    public ActivateAccessRequestCommand(
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        IApproverInboxNotifier approverInboxNotifier,
        IRequesterNotifier requesterNotifier,
        ISingleActiveLeaseEvaluator singleActiveLeaseEvaluator,
        IGoverningRuleResolver resolver,
        IAccessRuleEngine ruleEngine,
        ICurrentContext currentContext,
        IAccessAuditEventEmitter accessAuditEventEmitter)
    {
        _accessRequestRepository = accessRequestRepository;
        _accessLeaseRepository = accessLeaseRepository;
        _approverInboxNotifier = approverInboxNotifier;
        _requesterNotifier = requesterNotifier;
        _singleActiveLeaseEvaluator = singleActiveLeaseEvaluator;
        _resolver = resolver;
        _ruleEngine = ruleEngine;
        _currentContext = currentContext;
        _accessAuditEventEmitter = accessAuditEventEmitter;
    }

    public async Task<AccessLease> ActivateAsync(Guid userId, Guid requestId, DateTime now)
    {
        var request = await _accessRequestRepository.GetByIdAsync(requestId);

        // 404 for both missing and someone else's request, so the caller can't probe for requests they don't own.
        if (request is null || request.RequesterId != userId)
        {
            throw new NotFoundException();
        }

        // An extension never activates. It applied itself when it was approved -- AccessRequest_CreateApprovedExtension
        // pushed the parent lease's end out in place -- and the request row it leaves behind exists to carry the
        // justification, anchor the automatic decision, and cap the lease at one extension. That row is written
        // Approved and stays Approved (the status enum has no 'activated'; the produced lease is what records an
        // activation), so without this guard every remaining check below passes for it and a second, independent lease
        // mints for a credential the requester already holds one for. The window it would mint over is exactly the
        // extension period, when the parent is still live -- and revoking that parent is what clears the
        // single-active-lease guard, so a revoked requester could re-mint their own access. Refused here rather than
        // deferred to the mint proc so the caller gets the reason, not an opaque precondition failure.
        if (request.ExtensionOfLeaseId is not null)
        {
            throw new BadRequestException("This request extended an existing lease and cannot start a new one.");
        }

        // Activation is idempotent while the produced lease is live (double-click, a second tab racing the
        // auto-activating open flow); a revoked or lapsed lease is final — a request authorizes access at most once.
        var existing = await _accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        if (existing is not null)
        {
            if (existing.IsLive(now))
            {
                return existing;
            }
            throw new ConflictException("This request's access has already been used and is no longer active.");
        }

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
            // No Action is set: the lease is born running, and only an early end ever records one.
            // Activation mints the window the approver approved, exactly as the old approval-time path did; the
            // creation date is the activation audit timestamp (no decision row is written — approval was the
            // decision).
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            CreationDate = now,
        };
        lease.SetNewId();

        // The per-cipher singleton binds only when every path the caller reaches the cipher through is governed by a
        // singleton rule; an escape path leaves them unconstrained. The mint proc enforces it under a range lock.
        var enforceSingleActiveLease = await _singleActiveLeaseEvaluator.AppliesAsync(userId, request.CipherId);

        // audit (before/after): record the activation attempt, then the outcome around the point of no return. The
        // outcome kind follows the mint result -- a minted lease, or a recorded rejection (single-active-lease
        // conflict or a lost race). A race won by another activation is a no-op for this caller and emits nothing,
        // leaving the attempt as an in-doubt entry.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.LeaseActivated,
            OccurredAt = now,
            OrganizationId = request.OrganizationId,
            ActorId = userId,
            RequesterId = request.RequesterId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            AccessRequestId = request.Id,
            AccessLeaseId = lease.Id,
            LeaseNotBefore = lease.NotBefore,
            LeaseNotAfter = lease.NotAfter,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // The last gate before the point of no return: the rule's automated conditions have to still hold, now, at
        // the moment the lease is minted. Approval is a decision about *this* requester and window; a source-IP
        // allowlist is a standing condition on the network they reach the credential from, and nothing downstream
        // re-asks it -- CipherLeaseGate hands over a gated cipher on the existence of an active lease alone. Checking
        // only at submit meant an approval, once obtained, carried a caller across a narrowed allowlist or onto a
        // network the rule never admitted, for the whole approved window (PM-42273).
        var denial = await FindConditionDenialAsync(userId, request, now);
        if (denial is not null)
        {
            await _accessAuditEventEmitter.EmitAsync(
                audit with
                {
                    Kind = AccessAuditEventKind.LeaseActivationRejected,
                    Phase = AccessAuditEventPhase.Outcome,
                    AccessLeaseId = null,
                    // The reason, not the copy shown to the requester: wording is presentation and will be
                    // translated, while the reason stays queryable and means one thing to whoever reads the trail.
                    Detail = denial.Reason.ToString(),
                });
            throw new BadRequestException(AccessDenialMessage.For(denial));
        }

        var outcome = await _accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, enforceSingleActiveLease);

        if (outcome == AccessLeaseMintOutcome.SingleActiveLeaseConflict)
        {
            await _accessAuditEventEmitter.EmitAsync(
                audit with { Kind = AccessAuditEventKind.LeaseActivationRejected, Phase = AccessAuditEventPhase.Outcome, AccessLeaseId = null });
            throw new ConflictException("Another active lease exists for this item. Try again once it ends.");
        }

        if (outcome == AccessLeaseMintOutcome.PreconditionFailed)
        {
            // Lost a race: the guarded insert re-checks every precondition, so a miss means another activation won
            // or the request changed underneath us. If the winner's lease is live, activation still succeeded from
            // this caller's point of view.
            var winner = await _accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
            if (winner?.IsLive(now) == true)
            {
                return winner;
            }
            await _accessAuditEventEmitter.EmitAsync(
                audit with { Kind = AccessAuditEventKind.LeaseActivationRejected, Phase = AccessAuditEventPhase.Outcome, AccessLeaseId = null });
            throw new ConflictException("This request can no longer be activated.");
        }

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

        // The approver's history row just flipped approved -> activated and gained a revocable lease; tell every
        // approver of this collection to re-fetch, mirroring decide and revoke.
        await _approverInboxNotifier.NotifyCollectionApproversAsync(request.CollectionId);

        // Tell the requester's other devices the approved request just minted a lease, so their "My requests" view
        // and any open partial cipher pick up the live lease without a manual refresh.
        await _requesterNotifier.NotifyRequesterAsync(request.RequesterId);

        return lease;
    }

    /// <summary>
    /// Re-evaluates the governing rule's automated conditions against the caller's signals right now. Returns the
    /// denial to refuse with, or null when the conditions still admit the caller — or when there are none left to
    /// apply.
    /// </summary>
    /// <remarks>
    /// The rule pinned on the request is the one consulted, not whichever rule governs the cipher today: a request is
    /// held to the rule that approved it, and re-resolving could hand it a rule created or re-pointed since. Requests
    /// predating pinning fall back to resolution so the gate still covers them rather than waving them through.
    ///
    /// The approval gate itself is stripped (<see cref="GoverningRule.AutomatedConditions"/>) — an approver's verdict
    /// has already settled it, and re-asking would refuse every human-approved activation outright. That is also why a
    /// rule the server cannot parse is refused here rather than deferred: its fail-safe stand-in is an approval gate,
    /// and stripping that leaves nothing, which the engine reads as vacuously satisfied.
    /// </remarks>
    private async Task<AccessEvaluation?> FindConditionDenialAsync(Guid userId, AccessRequest request, DateTime now)
    {
        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));

        var governingRule = request.RuleId is { } ruleId
            ? await _resolver.ResolvePinnedAsync(ruleId, request.CollectionId)
            : await _resolver.ResolveAsync(userId, request.CipherId, signals);

        // No rule left to enforce: the admin disabled or deleted it, or the cipher is no longer reachable through a
        // gated collection. Leasing has stopped governing this credential, so there is nothing to hold the caller to
        // and the approved request activates.
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
            // No condition kind asks for approval outside the gate stripped above, but if one ever did it would be
            // asking for something this gate cannot deliver — the request is already approved and there is no second
            // approver to route to. Recorded as unsupported rather than passed off as a plain deny with no reason.
            AccessEvaluationOutcome.RequiresApproval => AccessEvaluation.Deny(DenyReason.UnsupportedCondition),
            _ => evaluation,
        };
    }
}

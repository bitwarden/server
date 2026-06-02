using Bit.Core.PrivilegedAccessManagement.Engine.Conditions;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.PrivilegedAccessManagement.Engine;

public sealed class AccessRuleEngine(
    TimeProvider time,
    IAccessRuleResolver ruleResolver,
    IAccessRuleRequestRepository requests,
    IAccessRuleLeaseRepository leases)
{
    private static readonly IReadOnlyList<IAccessCondition> _conditions =
    [
        new HumanApprovalCondition(),
        new IpRangeCondition(),
        new TimeOfDayCondition(),
    ];

    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly IAccessRuleResolver _ruleResolver = ruleResolver ?? throw new ArgumentNullException(nameof(ruleResolver));
    private readonly IAccessRuleRequestRepository _requests = requests ?? throw new ArgumentNullException(nameof(requests));
    private readonly IAccessRuleLeaseRepository _leases = leases ?? throw new ArgumentNullException(nameof(leases));

    /// <summary>
    /// Checks whether the user may access the cipher right now. Access is granted only when the user
    /// already holds a valid, unexpired lease; this method never evaluates rules or issues leases.
    /// </summary>
    public AccessRuleEngineResult Check(CipherDetails cipher, AccessRuleSignals signals)
    {
        if (!_leases.TryGet(cipher.Id, signals.Username, out var lease))
        {
            return AccessRuleEngineResult.Denied(DenyReason.NoLease);
        }

        if (lease.Expires <= _time.GetUtcNow())
        {
            return AccessRuleEngineResult.Denied(DenyReason.InvalidLease);
        }

        return AccessOutcome.Granted;
    }

    /// <summary>
    /// Requests access to a cipher by creating a pending request that can later be exchanged for a
    /// lease. Fails when the user already holds an active lease or already has a pending request.
    /// </summary>
    public RequestAccessResult RequestAccess(CipherDetails cipher, AccessRuleSignals signals)
    {
        // An active lease already grants access, so there is nothing to request.
        if (_leases.TryGet(cipher.Id, signals.Username, out var lease) && lease.Expires > _time.GetUtcNow())
        {
            return RequestAccessResult.Failed(RequestAccessFailReason.ExistingLease);
        }

        // A request is already pending for this user and cipher.
        if (_requests.TryGet(cipher.Id, signals.Username, out _))
        {
            return RequestAccessResult.Failed(RequestAccessFailReason.ExistingRequest);
        }

        var request = _requests.Create(cipher.Id, signals);
        return RequestAccessResult.Created(request);
    }

    /// <summary>
    /// Exchanges a pending request for a lease. Approval is an explicit gate; the rule is then
    /// re-evaluated against the signals captured when the request was made, and a lease is issued
    /// when access is granted and lease-issuance constraints are satisfied.
    /// </summary>
    public ExchangeResult ExchangeRequestForLease(CipherDetails cipher, string username)
    {
        if (!_requests.TryGet(cipher.Id, username, out var request))
        {
            return ExchangeResult.Failed(ExchangeFailReason.RequestNotFound);
        }

        var rule = _ruleResolver.Resolve(cipher);
        if (rule is null)
        {
            // No rule governs this cipher, so there is no policy under which to issue a lease.
            return ExchangeResult.Failed(ExchangeFailReason.NoRule);
        }

        // A rule that requires approval cannot be exchanged until the request has been approved.
        if (rule.RequireApproval && !request.Approved)
        {
            return ExchangeResult.Failed(ExchangeFailReason.NotApproved);
        }

        // Evaluate the rule against the signals captured at request time so the requester cannot
        // change their context (IP, time of day) between requesting and exchanging.
        var context = new AccessRuleEngineContext { Rule = rule, Signals = request.Signals };
        var decision = AccessDecision.Combine(_conditions.Select(condition => condition.Evaluate(context)));
        if (decision.Kind == DecisionKind.Deny)
        {
            return ExchangeResult.AccessDenied(decision.Reason);
        }

        // A singleton rule allows only one active lease per cipher at a time.
        if (rule.RequireSingleton && _leases.TryGet(cipher.Id, out var held) && held.Expires > _time.GetUtcNow())
        {
            return ExchangeResult.Failed(ExchangeFailReason.SingletonHeld);
        }

        if (!_leases.TryCreate(request, rule.Duration, out var lease))
        {
            return ExchangeResult.Failed(ExchangeFailReason.LeaseCreationFailed);
        }

        return ExchangeResult.Created(lease);
    }
}

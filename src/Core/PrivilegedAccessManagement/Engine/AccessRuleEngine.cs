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

    public AccessRuleEngineResult Check(CipherDetails cipher, AccessRuleSignals signals)
    {
        var rule = _ruleResolver.Resolve(cipher);
        if (rule == null)
        {
            // No rule means that we should grant access
            return AccessOutcome.Granted;
        }

        // An active, unexpired lease for this user grants access without re-evaluating
        if (_leases.TryGet(cipher.Id, signals.Username, out var lease) && lease.Expires > _time.GetUtcNow())
        {
            return AccessOutcome.Granted;
        }

        // Get an existing request, or create it
        if (!_requests.TryGet(cipher.Id, signals.Username, out var request))
        {
            request = _requests.Create(cipher.Id, signals.Username);
        }

        var context = new AccessRuleEngineContext { Rule = rule, Signals = signals };

        // Evaluate every condition and combine their decisions
        var decision = AccessDecision.Combine(_conditions.Select(condition => condition.Evaluate(context)));
        switch (decision.Kind)
        {
            case DecisionKind.Deny:
                return AccessRuleEngineResult.Denied(decision.Reason);
            case DecisionKind.RequiresApproval when !request.Approved:
                return AccessOutcome.RequiresApproval;
        }

        // Access is permitted; apply lease-issuance constraints and create the lease
        if (rule.RequireSingleton && _leases.TryGet(cipher.Id, out _))
        {
            return AccessRuleEngineResult.Denied(DenyReason.SingletonHeld);
        }

        // Create the lease
        if (!_leases.TryCreate(request, rule.Duration, out _))
        {
            return AccessOutcome.LeaseCreationFailed;
        }

        return AccessOutcome.Granted;
    }
}

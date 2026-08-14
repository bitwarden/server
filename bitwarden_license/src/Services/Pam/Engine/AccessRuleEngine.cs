using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Engine;

/// <summary>
/// Combines the results of an access rule's flat list of <see cref="AccessCondition"/>s into one decision. Each
/// condition evaluates itself (<see cref="AccessCondition.Evaluate"/>); the engine only folds those results, with
/// deny taking precedence over a pending approval, which in turn takes precedence over allow. An empty list is
/// vacuously satisfied (allow). Unparseable inputs fail closed before they reach the engine.
/// </summary>
public sealed class AccessRuleEngine : IAccessRuleEngine
{
    public AccessEvaluation Evaluate(IReadOnlyList<AccessCondition> conditions, AccessSignals signals) =>
        AccessEvaluation.Combine(conditions.Select(condition => EvaluateOne(condition, signals)));

    private static AccessEvaluation EvaluateOne(AccessCondition? condition, AccessSignals signals) =>
        // A null entry (malformed stored conditions) cannot be evaluated, so fail closed.
        condition is null ? AccessEvaluation.Deny(DenyReason.UnsupportedCondition) : condition.Evaluate(signals);
}

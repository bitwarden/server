using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Engine;

/// <summary>
/// Folds the results of an access rule's <see cref="AccessCondition"/>s, each evaluated by the condition itself, into
/// one decision. Unparseable rules fail closed before they reach the engine.
/// </summary>
public sealed class AccessRuleEngine : IAccessRuleEngine
{
    public AccessEvaluation Evaluate(IReadOnlyList<AccessCondition> conditions, AccessSignals signals) =>
        AccessEvaluation.Combine(conditions.Select(condition => EvaluateOne(condition, signals)));

    private static AccessEvaluation EvaluateOne(AccessCondition? condition, AccessSignals signals) =>
        // A null entry (malformed stored conditions) cannot be evaluated, so fail closed.
        condition is null ? AccessEvaluation.Deny(DenyReason.UnsupportedCondition) : condition.Evaluate(signals);
}

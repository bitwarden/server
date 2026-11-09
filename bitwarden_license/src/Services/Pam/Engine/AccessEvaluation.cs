namespace Bit.Services.Pam.Engine;

/// <summary>The result of one access condition, or of a rule's whole condition list.</summary>
public enum AccessEvaluationOutcome
{
    Allow,

    RequiresApproval,

    Deny,
}

/// <summary>Why an evaluation denied; <see cref="None"/> for any other outcome.</summary>
public enum DenyReason
{
    None = 0,

    /// <summary>The caller's IP was absent, the allowlist empty, or the IP outside every listed CIDR.</summary>
    NotWithinIpRange,

    /// <summary>
    /// A condition could not be evaluated or applied (a malformed stored document, or an approval demand with no
    /// approver to route to), so access fails closed.
    /// </summary>
    UnsupportedCondition,

    /// <summary>
    /// The request fell outside every window of a time-of-day condition, or its timezone could not be resolved.
    /// </summary>
    NotWithinTimeWindow,
}

/// <summary>The outcome of a condition or a combined rule, with the <see cref="Reason"/> for a denial.</summary>
public sealed record AccessEvaluation
{
    public required AccessEvaluationOutcome Outcome { get; init; }

    public DenyReason Reason { get; init; } = DenyReason.None;

    public static AccessEvaluation Allow { get; } = new() { Outcome = AccessEvaluationOutcome.Allow };

    public static AccessEvaluation RequiresApproval { get; } = new() { Outcome = AccessEvaluationOutcome.RequiresApproval };

    public static AccessEvaluation Deny(DenyReason reason) => new()
    {
        Outcome = AccessEvaluationOutcome.Deny,
        Reason = reason
    };

    /// <summary>
    /// Folds per-condition evaluations into one with deny &gt; requires-approval &gt; allow precedence, returning the
    /// first deny as-is. An empty sequence returns <see cref="Allow"/>.
    /// </summary>
    public static AccessEvaluation Combine(IEnumerable<AccessEvaluation> evaluations)
    {
        var requiresApproval = false;

        foreach (var evaluation in evaluations)
        {
            switch (evaluation.Outcome)
            {
                case AccessEvaluationOutcome.Deny:
                    return evaluation;
                case AccessEvaluationOutcome.RequiresApproval:
                    requiresApproval = true;
                    break;
            }
        }

        return requiresApproval ? RequiresApproval : Allow;
    }
}

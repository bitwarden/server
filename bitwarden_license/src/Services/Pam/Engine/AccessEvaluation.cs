namespace Bit.Services.Pam.Engine;

/// <summary>
/// The result of evaluating a single access condition, or the combined result of a rule's whole condition list.
/// </summary>
public enum AccessEvaluationOutcome
{
    /// <summary>Access is granted automatically, with no human decision required.</summary>
    Allow,

    /// <summary>A human decision is required before a lease may be issued.</summary>
    RequiresApproval,

    /// <summary>Access is refused; the accompanying <see cref="DenyReason"/> records why.</summary>
    Deny,
}

/// <summary>
/// Why an evaluation denied. Carried on a deny <see cref="AccessEvaluation"/>; <see cref="None"/> is the default
/// for any non-deny outcome.
/// </summary>
public enum DenyReason
{
    /// <summary>Not a denial (the outcome is allow or requires-approval).</summary>
    None = 0,

    /// <summary>The caller's IP was absent, the allowlist was empty, or the IP fell outside every listed CIDR.</summary>
    NotWithinIpRange,

    /// <summary>The timezone was unknown/invalid, or the instant fell outside every configured window.</summary>
    NotWithinTimeWindow,

    /// <summary>
    /// A condition entry could not be evaluated — in practice a null entry from a malformed stored document — so it
    /// fails closed. A genuinely unknown <c>kind</c> cannot reach here: the JSON layer rejects unknown kinds and the
    /// visitor dispatch is exhaustive at compile time.
    /// </summary>
    UnsupportedCondition,
}

/// <summary>
/// The outcome of evaluating an access condition (or a combined rule result): an <see cref="Outcome"/> plus, when
/// it is a denial, the <see cref="Reason"/>. Build instances via <see cref="Allow"/>, <see cref="RequiresApproval"/>,
/// or <see cref="Deny"/>, and fold a sequence together with <see cref="Combine"/>.
/// </summary>
public sealed record AccessEvaluation
{
    /// <summary>The evaluation's verdict.</summary>
    public required AccessEvaluationOutcome Outcome { get; init; }

    /// <summary>Why access was denied; <see cref="DenyReason.None"/> unless <see cref="Outcome"/> is <see cref="AccessEvaluationOutcome.Deny"/>.</summary>
    public DenyReason Reason { get; init; } = DenyReason.None;

    /// <summary>A shared allow result.</summary>
    public static AccessEvaluation Allow { get; } = new() { Outcome = AccessEvaluationOutcome.Allow };

    /// <summary>A shared requires-approval result.</summary>
    public static AccessEvaluation RequiresApproval { get; } = new() { Outcome = AccessEvaluationOutcome.RequiresApproval };

    /// <summary>Builds a deny result carrying the given <paramref name="reason"/>.</summary>
    public static AccessEvaluation Deny(DenyReason reason) => new()
    {
        Outcome = AccessEvaluationOutcome.Deny,
        Reason = reason
    };

    /// <summary>
    /// Folds a sequence of per-condition evaluations into one, with <b>deny &gt; requires-approval &gt; allow</b>
    /// precedence: the first deny short-circuits and is returned as-is; otherwise any requires-approval wins over
    /// allow. An empty sequence is vacuously satisfied and returns <see cref="Allow"/>.
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

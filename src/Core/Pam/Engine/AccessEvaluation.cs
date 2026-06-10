namespace Bit.Core.Pam.Engine;

public enum AccessEvaluationOutcome
{
    Allow,
    RequiresApproval,
    Deny,
}

public enum DenyReason
{
    None = 0,
    NotWithinIpRange,
    NotWithinTimeWindow,
    UnsupportedCondition,
}

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

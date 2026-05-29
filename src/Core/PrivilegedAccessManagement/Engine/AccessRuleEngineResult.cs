namespace Bit.Core.PrivilegedAccessManagement.Engine;

public sealed record AccessRuleEngineResult(AccessOutcome Outcome, DenyReason Reason = DenyReason.None)
{
    public static implicit operator AccessRuleEngineResult(AccessOutcome outcome) => new(outcome);

    public static AccessRuleEngineResult Denied(DenyReason reason) => new(AccessOutcome.Denied, reason);
}

public enum AccessOutcome
{
    Granted,
    Denied,
    RequiresApproval,
    LeaseCreationFailed,
}

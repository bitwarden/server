namespace Bit.Core.PrivilegedAccessManagement.Engine;

public sealed record ExchangeResult(
    ExchangeOutcome Outcome,
    AccessRuleLease? Lease = null,
    ExchangeFailReason FailReason = ExchangeFailReason.None,
    DenyReason DenyReason = DenyReason.None)
{
    public static ExchangeResult Created(AccessRuleLease lease) =>
        new(ExchangeOutcome.Created, Lease: lease);

    public static ExchangeResult Failed(ExchangeFailReason reason) =>
        new(ExchangeOutcome.Failed, FailReason: reason);

    public static ExchangeResult AccessDenied(DenyReason denyReason) =>
        new(ExchangeOutcome.Failed, FailReason: ExchangeFailReason.AccessDenied, DenyReason: denyReason);
}

public enum ExchangeOutcome
{
    Created,
    Failed,
}

public enum ExchangeFailReason
{
    None = 0,
    RequestNotFound,
    NoRule,
    NotApproved,
    AccessDenied,
    SingletonHeld,
    LeaseCreationFailed,
}

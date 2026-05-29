namespace Bit.Core.PrivilegedAccessManagement.Engine;

public sealed record ExchangeResult(
    ExchangeOutcome Outcome,
    AccessRuleLease? Lease = null,
    ExchangeFailReason FailReason = ExchangeFailReason.None)
{
    public static ExchangeResult Created(AccessRuleLease lease) =>
        new(ExchangeOutcome.Created, Lease: lease);

    public static ExchangeResult Failed(ExchangeFailReason reason) =>
        new(ExchangeOutcome.Failed, FailReason: reason);
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
    SingletonHeld,
    LeaseCreationFailed,
}

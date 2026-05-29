namespace Bit.Core.PrivilegedAccessManagement.Engine;

public sealed record RequestAccessResult(
    RequestAccessOutcome Outcome,
    AccessRuleRequest? Request = null,
    RequestAccessFailReason FailReason = RequestAccessFailReason.None,
    DenyReason DenyReason = DenyReason.None)
{
    public static RequestAccessResult Created(AccessRuleRequest request) =>
        new(RequestAccessOutcome.Created, Request: request);

    public static RequestAccessResult Failed(RequestAccessFailReason reason) =>
        new(RequestAccessOutcome.Failed, FailReason: reason);

    public static RequestAccessResult AccessDenied(DenyReason denyReason) =>
        new(RequestAccessOutcome.Failed, FailReason: RequestAccessFailReason.AccessDenied, DenyReason: denyReason);
}

public enum RequestAccessOutcome
{
    Created,
    Failed,
}

public enum RequestAccessFailReason
{
    None = 0,
    ExistingLease,
    ExistingRequest,
    NoRule,
    AccessDenied,
}

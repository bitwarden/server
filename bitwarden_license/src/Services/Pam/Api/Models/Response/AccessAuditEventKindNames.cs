using Bit.Pam.Enums;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// Maps <see cref="AccessAuditEventKind"/> to the string vocabulary the governance client expects, mirroring
/// <see cref="AccessLeaseStatusNames"/> for leases. The projection emits the request, lease, and rule administration
/// kinds today; the remaining names (credential access, system controls) are defined so the contract stays stable as
/// those kinds come online.
/// </summary>
public static class AccessAuditEventKindNames
{
    public const string RequestSubmitted = "requestSubmitted";
    public const string RequestApproved = "requestApproved";
    public const string RequestDenied = "requestDenied";
    public const string RequestCancelled = "requestCancelled";
    public const string RequestExpiredUnanswered = "requestExpiredUnanswered";
    public const string RequestExpiredUnactivated = "requestExpiredUnactivated";
    public const string LeaseActivated = "leaseActivated";
    public const string LeaseActivationRejected = "leaseActivationRejected";
    public const string LeaseExtended = "leaseExtended";
    public const string LeaseRevoked = "leaseRevoked";
    public const string LeaseExpired = "leaseExpired";
    public const string CredentialAccessed = "credentialAccessed";
    public const string CredentialAccessDenied = "credentialAccessDenied";
    public const string RuleCreated = "ruleCreated";
    public const string RuleUpdated = "ruleUpdated";
    public const string RuleDeleted = "ruleDeleted";
    public const string LeasingKillSwitchTriggered = "leasingKillSwitchTriggered";
    public const string LeasingFreezeEnabled = "leasingFreezeEnabled";
    public const string LeasingFreezeLifted = "leasingFreezeLifted";

    public static string From(AccessAuditEventKind kind) => kind switch
    {
        AccessAuditEventKind.RequestSubmitted => RequestSubmitted,
        AccessAuditEventKind.RequestApproved => RequestApproved,
        AccessAuditEventKind.RequestDenied => RequestDenied,
        AccessAuditEventKind.RequestCancelled => RequestCancelled,
        AccessAuditEventKind.RequestExpiredUnanswered => RequestExpiredUnanswered,
        AccessAuditEventKind.RequestExpiredUnactivated => RequestExpiredUnactivated,
        AccessAuditEventKind.LeaseActivated => LeaseActivated,
        AccessAuditEventKind.LeaseActivationRejected => LeaseActivationRejected,
        AccessAuditEventKind.LeaseExtended => LeaseExtended,
        AccessAuditEventKind.LeaseRevoked => LeaseRevoked,
        AccessAuditEventKind.LeaseExpired => LeaseExpired,
        AccessAuditEventKind.CredentialAccessed => CredentialAccessed,
        AccessAuditEventKind.CredentialAccessDenied => CredentialAccessDenied,
        AccessAuditEventKind.RuleCreated => RuleCreated,
        AccessAuditEventKind.RuleUpdated => RuleUpdated,
        AccessAuditEventKind.RuleDeleted => RuleDeleted,
        AccessAuditEventKind.LeasingKillSwitchTriggered => LeasingKillSwitchTriggered,
        AccessAuditEventKind.LeasingFreezeEnabled => LeasingFreezeEnabled,
        AccessAuditEventKind.LeasingFreezeLifted => LeasingFreezeLifted,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

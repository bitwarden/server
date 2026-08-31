using Bit.Pam.Enums;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// Maps <see cref="AccessAuditEventKind"/> to the string vocabulary the governance client expects. The projection
/// emits the request, lease, rule administration, and rotation-lifecycle/fleet-administration kinds today; the
/// remaining names (credential access, system controls) are defined so the contract stays stable as those kinds come
/// online.
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
    public const string RotationConfigCreated = "rotationConfigCreated";
    public const string RotationSettingsUpdated = "rotationSettingsUpdated";
    public const string RotationAccountUpdated = "rotationAccountUpdated";
    public const string RotationPaused = "rotationPaused";
    public const string RotationResumed = "rotationResumed";
    public const string RotationConfigDeleted = "rotationConfigDeleted";
    public const string RotationOffered = "rotationOffered";
    public const string RotationDispatched = "rotationDispatched";
    public const string RotationSucceeded = "rotationSucceeded";
    public const string RotationAttemptFailed = "rotationAttemptFailed";
    public const string RotationFailed = "rotationFailed";
    public const string RotationJobReleased = "rotationJobReleased";
    public const string RotationJobTimedOut = "rotationJobTimedOut";
    public const string RotationCipherWriteRejected = "rotationCipherWriteRejected";
    public const string RotationReportRejected = "rotationReportRejected";
    public const string ManualRotationDue = "manualRotationDue";
    public const string ManualRotationRecorded = "manualRotationRecorded";
    public const string DaemonRegistered = "daemonRegistered";
    public const string DaemonRevoked = "daemonRevoked";
    public const string DaemonDisabled = "daemonDisabled";
    public const string DaemonEnabled = "daemonEnabled";
    public const string DaemonDeleted = "daemonDeleted";
    public const string DaemonAssignedToTarget = "daemonAssignedToTarget";
    public const string DaemonUnassignedFromTarget = "daemonUnassignedFromTarget";
    public const string TargetSystemRegistered = "targetSystemRegistered";
    public const string TargetSystemDisabled = "targetSystemDisabled";
    public const string TargetSystemEnabled = "targetSystemEnabled";
    public const string TargetSystemRenamed = "targetSystemRenamed";
    public const string TargetSystemPolicyUpdated = "targetSystemPolicyUpdated";

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
        AccessAuditEventKind.RotationConfigCreated => RotationConfigCreated,
        AccessAuditEventKind.RotationSettingsUpdated => RotationSettingsUpdated,
        AccessAuditEventKind.RotationAccountUpdated => RotationAccountUpdated,
        AccessAuditEventKind.RotationPaused => RotationPaused,
        AccessAuditEventKind.RotationResumed => RotationResumed,
        AccessAuditEventKind.RotationConfigDeleted => RotationConfigDeleted,
        AccessAuditEventKind.RotationOffered => RotationOffered,
        AccessAuditEventKind.RotationDispatched => RotationDispatched,
        AccessAuditEventKind.RotationSucceeded => RotationSucceeded,
        AccessAuditEventKind.RotationAttemptFailed => RotationAttemptFailed,
        AccessAuditEventKind.RotationFailed => RotationFailed,
        AccessAuditEventKind.RotationJobReleased => RotationJobReleased,
        AccessAuditEventKind.RotationJobTimedOut => RotationJobTimedOut,
        AccessAuditEventKind.RotationCipherWriteRejected => RotationCipherWriteRejected,
        AccessAuditEventKind.RotationReportRejected => RotationReportRejected,
        AccessAuditEventKind.ManualRotationDue => ManualRotationDue,
        AccessAuditEventKind.ManualRotationRecorded => ManualRotationRecorded,
        AccessAuditEventKind.DaemonRegistered => DaemonRegistered,
        AccessAuditEventKind.DaemonRevoked => DaemonRevoked,
        AccessAuditEventKind.DaemonDisabled => DaemonDisabled,
        AccessAuditEventKind.DaemonEnabled => DaemonEnabled,
        AccessAuditEventKind.DaemonDeleted => DaemonDeleted,
        AccessAuditEventKind.DaemonAssignedToTarget => DaemonAssignedToTarget,
        AccessAuditEventKind.DaemonUnassignedFromTarget => DaemonUnassignedFromTarget,
        AccessAuditEventKind.TargetSystemRegistered => TargetSystemRegistered,
        AccessAuditEventKind.TargetSystemDisabled => TargetSystemDisabled,
        AccessAuditEventKind.TargetSystemEnabled => TargetSystemEnabled,
        AccessAuditEventKind.TargetSystemRenamed => TargetSystemRenamed,
        AccessAuditEventKind.TargetSystemPolicyUpdated => TargetSystemPolicyUpdated,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

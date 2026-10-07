namespace Bit.Pam.Enums;

// The kinds of event in the PAM access-audit trail, grouped by subject with each group starting at a multiple of 10.
// Kinds marked "not emitted yet" have no caller.
public enum AccessAuditEventKind : byte
{
    // Access requests
    RequestSubmitted = 0,
    RequestApproved = 1,
    RequestDenied = 2,
    RequestCancelled = 3,
    RequestExpiredUnanswered = 4, // not emitted yet
    RequestExpiredUnactivated = 5, // not emitted yet

    // Access leases
    LeaseActivated = 10,
    LeaseActivationRejected = 11,
    LeaseExtended = 12,
    LeaseRevoked = 13,
    LeaseExpired = 14,

    // Credential access
    CredentialAccessed = 20, // not emitted yet
    CredentialAccessDenied = 21, // not emitted yet

    // Rule administration
    RuleCreated = 30,
    RuleUpdated = 31,
    RuleDeleted = 32,

    // System controls
    LeasingKillSwitchTriggered = 40, // not emitted yet
    LeasingFreezeEnabled = 41, // not emitted yet
    LeasingFreezeLifted = 42, // not emitted yet

    // Rotation lifecycle
    RotationConfigCreated = 50,

    /// <summary>A rotation config's schedule settings were updated.</summary>
    RotationSettingsUpdated = 51,

    /// <summary>A rotation config's target, account, or termination settings were updated.</summary>
    RotationAccountUpdated = 52,

    RotationPaused = 53,

    RotationResumed = 54,

    RotationConfigDeleted = 55,

    /// <summary>A rotation job was created for a config.</summary>
    RotationOffered = 56,

    /// <summary>A rotation job was claimed by an access connector.</summary>
    RotationDispatched = 57,

    RotationSucceeded = 58,

    /// <summary>A rotation attempt failed and the job will retry.</summary>
    RotationAttemptFailed = 59,

    /// <summary>A rotation job failed after exhausting its retries.</summary>
    RotationFailed = 60,

    /// <summary>A claimed rotation job was released back to pending after its access connector went stale.</summary>
    RotationJobReleased = 61,

    RotationJobTimedOut = 62,

    RotationCipherWriteRejected = 63,

    /// <summary>A stale rotation success or failure report was rejected.</summary>
    RotationReportRejected = 64,

    ManualRotationDue = 65,

    /// <summary>An admin recorded a manual rotation as completed.</summary>
    ManualRotationRecorded = 66,

    // Access connector and target administration
    AccessConnectorRegistered = 70,

    /// <summary>Not emitted; stored events may still carry it.</summary>
    AccessConnectorRevoked = 71,

    AccessConnectorAssignedToTarget = 72,

    AccessConnectorUnassignedFromTarget = 73,

    TargetSystemRegistered = 74,

    TargetSystemDisabled = 75,

    TargetSystemEnabled = 76,

    TargetSystemRenamed = 77,

    /// <summary>A target system's password policy or session-termination capability was updated.</summary>
    TargetSystemPolicyUpdated = 78,

    AccessConnectorDisabled = 79,

    AccessConnectorEnabled = 80,

    AccessConnectorDeleted = 81,

    /// <summary>A target system was deleted along with its access connector assignments.</summary>
    TargetSystemDeleted = 82,
}

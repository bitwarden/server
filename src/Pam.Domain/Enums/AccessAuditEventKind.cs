namespace Bit.Pam.Enums;

// The kinds of event in the PAM access-audit trail, grouped by subject and incremented by 10 per group.
// Kinds marked "not emitted yet" have no caller: the expiry kinds are time-derived and need a background sweep, the
// credential-access kinds need a per-open payload, and the leasing controls are out of scope this pass.
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
    /// <summary>A rotation config was created for a cipher.</summary>
    RotationConfigCreated = 50,

    /// <summary>A rotation config's schedule settings were updated.</summary>
    RotationSettingsUpdated = 51,

    /// <summary>A rotation config's target, account, or termination settings were updated.</summary>
    RotationAccountUpdated = 52,

    /// <summary>A rotation config was paused.</summary>
    RotationPaused = 53,

    /// <summary>A rotation config was resumed.</summary>
    RotationResumed = 54,

    /// <summary>A rotation config was deleted.</summary>
    RotationConfigDeleted = 55,

    /// <summary>A rotation job was created for a config.</summary>
    RotationOffered = 56,

    /// <summary>A rotation job was claimed by an access connector.</summary>
    RotationDispatched = 57,

    /// <summary>A rotation job succeeded.</summary>
    RotationSucceeded = 58,

    /// <summary>A rotation attempt failed and the job will retry.</summary>
    RotationAttemptFailed = 59,

    /// <summary>A rotation job failed after exhausting its retries.</summary>
    RotationFailed = 60,

    /// <summary>A claimed rotation job was released back to pending after its access connector went stale.</summary>
    RotationJobReleased = 61,

    /// <summary>A rotation job timed out with no successful attempt.</summary>
    RotationJobTimedOut = 62,

    /// <summary>An access connector's cipher write was rejected.</summary>
    RotationCipherWriteRejected = 63,

    /// <summary>A stale rotation success or failure report was rejected.</summary>
    RotationReportRejected = 64,

    /// <summary>A manual rotation became due.</summary>
    ManualRotationDue = 65,

    /// <summary>An admin recorded a manual rotation as completed.</summary>
    ManualRotationRecorded = 66,

    // Access connector and target administration
    /// <summary>An access connector was registered.</summary>
    AccessConnectorRegistered = 70,

    /// <summary>An access connector was revoked. No longer emitted.</summary>
    AccessConnectorRevoked = 71,

    /// <summary>An access connector was assigned to a target system.</summary>
    AccessConnectorAssignedToTarget = 72,

    /// <summary>An access connector was unassigned from a target system.</summary>
    AccessConnectorUnassignedFromTarget = 73,

    /// <summary>A target system was registered.</summary>
    TargetSystemRegistered = 74,

    /// <summary>A target system was disabled.</summary>
    TargetSystemDisabled = 75,

    /// <summary>A target system was enabled.</summary>
    TargetSystemEnabled = 76,

    /// <summary>A target system was renamed.</summary>
    TargetSystemRenamed = 77,

    /// <summary>A target system's password policy or session-termination capability was updated.</summary>
    TargetSystemPolicyUpdated = 78,

    /// <summary>An access connector was disabled.</summary>
    AccessConnectorDisabled = 79,

    /// <summary>A disabled access connector was re-enabled.</summary>
    AccessConnectorEnabled = 80,

    /// <summary>An access connector was permanently deleted.</summary>
    AccessConnectorDeleted = 81,

    /// <summary>A target system was permanently deleted, along with its access connector assignments.</summary>
    TargetSystemDeleted = 82,
}

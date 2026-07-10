namespace Bit.Pam.Enums;

/// <summary>
/// The kinds of event in the PAM access-audit trail. State-changing PAM actions write these events to a dedicated store
/// (<see cref="Models.AccessAuditEventData"/>); the trail is read back from it. Kinds are grouped by subject and
/// numbered in ranges per group, leaving room to grow. Some kinds are deferred — no action emits them yet (the
/// time-derived expiry kinds need a background sweep); see the per-member notes.
/// </summary>
public enum AccessAuditEventKind : byte
{
    // Access requests
    RequestSubmitted = 0,
    RequestApproved = 1,
    RequestDenied = 2,
    RequestCancelled = 3,

    /// <summary>Deferred: no action emits this — a pending request lapsing is time-derived and needs a sweep.</summary>
    RequestExpiredUnanswered = 4,

    /// <summary>Deferred: an approved request whose activation window lapsed without minting a lease. Time-derived; needs a sweep.</summary>
    RequestExpiredUnactivated = 5,

    // Access leases
    LeaseActivated = 10,

    /// <summary>A refused activation (single-active-lease conflict or a lost activation race); emitted by the activation command.</summary>
    LeaseActivationRejected = 11,

    LeaseExtended = 12,
    LeaseRevoked = 13,

    /// <summary>Deferred: an active lease reaching its end. Time-derived; needs a sweep.</summary>
    LeaseExpired = 14,

    // Credential access
    /// <summary>Deferred: opening a leased credential is a repeated event with no per-open footprint (future JSON).</summary>
    CredentialAccessed = 20,

    /// <summary>Deferred: a blocked read changes no state and repeats (future JSON).</summary>
    CredentialAccessDenied = 21,

    // Rule administration
    RuleCreated = 30,
    RuleUpdated = 31,

    /// <summary>A rule was soft-deleted; actor = <c>AccessRule.DeletedBy</c>, occurring at <c>DeletedDate</c>.</summary>
    RuleDeleted = 32,

    // System controls
    /// <summary>Deferred: out of scope this pass.</summary>
    LeasingKillSwitchTriggered = 40,

    /// <summary>Deferred: out of scope this pass.</summary>
    LeasingFreezeEnabled = 41,

    /// <summary>Deferred: out of scope this pass.</summary>
    LeasingFreezeLifted = 42,

    // Rotation lifecycle
    /// <summary>A rotation config was created for a cipher. Spec outcome <c>config_created</c>.</summary>
    RotationConfigCreated = 50,

    /// <summary>A rotation config's schedule/rotate-on-access-end settings were updated. Spec outcome <c>settings_updated</c>.</summary>
    RotationSettingsUpdated = 51,

    /// <summary>A rotation config's target/account/termination settings were updated. Spec outcome <c>account_updated</c>.</summary>
    RotationAccountUpdated = 52,

    /// <summary>A rotation config was paused. Spec outcome <c>paused</c>.</summary>
    RotationPaused = 53,

    /// <summary>A rotation config was resumed. Spec outcome <c>resumed</c>.</summary>
    RotationResumed = 54,

    /// <summary>A rotation config was deleted. Spec outcome <c>config_deleted</c>.</summary>
    RotationConfigDeleted = 55,

    /// <summary>A rotation job was created for a config (the single creation point, <c>OfferRotation</c>). Spec outcome <c>offered</c>.</summary>
    RotationOffered = 56,

    /// <summary>A rotation job was claimed by a daemon. Spec outcome <c>dispatched</c>.</summary>
    RotationDispatched = 57,

    /// <summary>A rotation job succeeded. Spec outcome <c>succeeded</c>.</summary>
    RotationSucceeded = 58,

    /// <summary>A rotation attempt failed but the job still has retry budget left. Spec outcome <c>attempt_failed</c>.</summary>
    RotationAttemptFailed = 59,

    /// <summary>A rotation job failed after exhausting its retry budget. Spec outcome <c>failed</c>.</summary>
    RotationFailed = 60,

    /// <summary>A claimed rotation job was released back to Pending by the sweep (stale daemon heartbeat past the claim lease). Spec outcome <c>released</c>.</summary>
    RotationJobReleased = 61,

    /// <summary>A rotation job timed out past its TTL with no successful attempt. Spec outcome <c>timed_out</c>.</summary>
    RotationJobTimedOut = 62,

    /// <summary>A daemon's cipher write was rejected by the atomic write-capability check. Spec outcome <c>write_rejected</c>.</summary>
    RotationCipherWriteRejected = 63,

    /// <summary>A stale success/failure report was rejected (attempt no longer executing, or claimant mismatch). Spec outcome <c>report_rejected</c>.</summary>
    RotationReportRejected = 64,

    /// <summary>A manual-method rotation config's obligation became due. Spec outcome <c>manual_rotation_due</c>.</summary>
    ManualRotationDue = 65,

    /// <summary>An admin recorded a manual rotation as completed. Spec outcome <c>manual_recorded</c>.</summary>
    ManualRotationRecorded = 66,

    // 67-69 reserved for rotation-lifecycle growth.

    // Deferred: no kind is allocated yet for the spec's access_end_deferred (the pending-access-end latch),
    // auto_paused (§6.8 failure-policy auto-pause), or daemon_credential_reissued (ReissueDaemonCredential) outcomes —
    // all out of scope this pass. Values are intentionally left unassigned rather than reserved, since the shape of
    // that work (and which range it belongs in) isn't settled yet.

    // Fleet / target administration
    /// <summary>A rotation daemon was registered. Spec outcome <c>daemon_registered</c>.</summary>
    DaemonRegistered = 70,

    /// <summary>
    /// A rotation daemon was revoked. Legacy: the revoke action was replaced by the reversible disable/enable pair
    /// plus a permanent delete (see <see cref="DaemonDisabled"/>, <see cref="DaemonEnabled"/>,
    /// <see cref="DaemonDeleted"/>); no action emits this anymore, but it is retained so historical rows still read.
    /// </summary>
    DaemonRevoked = 71,

    /// <summary>A daemon was assigned to a target system. Spec outcome <c>daemon_assigned</c>.</summary>
    DaemonAssignedToTarget = 72,

    /// <summary>A daemon was unassigned from a target system. Spec outcome <c>daemon_unassigned</c>.</summary>
    DaemonUnassignedFromTarget = 73,

    /// <summary>A target system was registered (automatic or manual). Spec outcome <c>target_registered</c>.</summary>
    TargetSystemRegistered = 74,

    /// <summary>A target system was disabled. Spec outcome <c>target_disabled</c>.</summary>
    TargetSystemDisabled = 75,

    /// <summary>A target system was enabled. Spec outcome <c>target_enabled</c>.</summary>
    TargetSystemEnabled = 76,

    /// <summary>A target system was renamed. Spec outcome <c>target_renamed</c>.</summary>
    TargetSystemRenamed = 77,

    /// <summary>A target system's password policy or session-termination capability was updated. Spec outcome <c>target_policy_updated</c>.</summary>
    TargetSystemPolicyUpdated = 78,

    // Daemon lifecycle (continued). The fleet range above (70-73) is full, so the disable/enable/delete kinds that
    // replaced revoke continue here.

    /// <summary>A rotation daemon was disabled (reversible pause; credential retained).</summary>
    DaemonDisabled = 79,

    /// <summary>A disabled rotation daemon was re-enabled.</summary>
    DaemonEnabled = 80,

    /// <summary>A rotation daemon was permanently deleted (row removed and its credential invalidated).</summary>
    DaemonDeleted = 81,
}

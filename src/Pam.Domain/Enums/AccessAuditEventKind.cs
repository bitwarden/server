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

    /// <summary>A rule was hard-deleted; the event carries the actor and rule name since the row does not survive.</summary>
    RuleDeleted = 32,

    // System controls
    /// <summary>Deferred: out of scope this pass.</summary>
    LeasingKillSwitchTriggered = 40,

    /// <summary>Deferred: out of scope this pass.</summary>
    LeasingFreezeEnabled = 41,

    /// <summary>Deferred: out of scope this pass.</summary>
    LeasingFreezeLifted = 42,
}

namespace Bit.Pam.Enums;

/// <summary>
/// The kinds of event in the synthesized PAM access-audit trail. The trail is a read model projected from existing PAM
/// entity state (<see cref="Entities.AccessRequest"/>, <see cref="Entities.AccessLease"/>,
/// <see cref="Entities.AccessRule"/> and <see cref="Entities.AccessDecision"/>) — there is no stored audit record. Kinds
/// are grouped by subject and numbered in ranges per group, leaving room to grow. Some kinds are not yet emitted; see
/// the per-member notes.
/// </summary>
public enum AccessAuditEventKind : byte
{
    // Access requests
    RequestSubmitted = 0,
    RequestApproved = 1,
    RequestDenied = 2,
    RequestCancelled = 3,
    RequestExpiredUnanswered = 4,

    /// <summary>An approved request whose activation window lapsed without ever minting a lease. Derived (no new state).</summary>
    RequestExpiredUnactivated = 5,

    // Access leases
    LeaseActivated = 10,

    /// <summary>A refused activation (single-active-lease conflict or a lost activation race), recorded as <c>AccessRequest.RejectedDate</c>.</summary>
    LeaseActivationRejected = 11,

    LeaseExtended = 12,
    LeaseRevoked = 13,
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
}

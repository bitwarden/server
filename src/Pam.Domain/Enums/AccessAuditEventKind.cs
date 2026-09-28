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
    LeaseExpired = 14, // not emitted yet

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
}

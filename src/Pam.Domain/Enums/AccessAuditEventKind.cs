namespace Bit.Pam.Enums;

/// <summary>
/// The kinds of event in the PAM access-audit trail. State-changing PAM actions write these events to a dedicated store
/// (<see cref="Models.AccessAuditEventData"/>); the trail is read back from it. Kinds are grouped by subject and
/// numbered in ranges per group, leaving room to grow; only the rule-administration group (0-9) exists so far, with the
/// request, lease, credential, and system-control groups added as the actions that emit them land.
/// </summary>
public enum AccessAuditEventKind : byte
{
    // Rule administration
    RuleCreated = 0,
    RuleUpdated = 1,

    /// <summary>A rule was hard-deleted; the event carries the actor and rule name since the row does not survive.</summary>
    RuleDeleted = 2,
}

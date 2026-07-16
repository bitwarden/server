using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// The write-side payload for a PAM audit event: the raw facts a state-changing action records at the moment it
/// happens, before anything is stored. It carries only the event itself — its <see cref="Kind"/> and
/// <see cref="Phase"/>, when it occurred, the organization, the actor, and the subject rule. Display fields are joined
/// in on read rather than captured here. Emitted through the PAM audit-event emitter.
/// </summary>
public record AccessAuditEventData
{
    public required AccessAuditEventKind Kind { get; init; }

    /// <summary>Whether this is the pre-action attempt record or the post-action outcome record (before/after model).</summary>
    public AccessAuditEventPhase Phase { get; init; } = AccessAuditEventPhase.Outcome;

    public required DateTime OccurredAt { get; init; }
    public required Guid OrganizationId { get; init; }

    /// <summary>Who performed the event; null for a system / automatic event (an automatic decision).</summary>
    public Guid? ActorId { get; init; }

    public Guid? AccessRuleId { get; init; }

    /// <summary>
    /// The access rule's name, supplied by the rule commands (which hold the entity). Captured in C# because a rule can
    /// be hard-deleted in the same action, after which a JOIN could no longer resolve it. Null for non-rule events.
    /// </summary>
    public string? RuleName { get; init; }
}

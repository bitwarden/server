using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// The write-side payload for a PAM audit event: the raw facts a state-changing action records at the moment it
/// happens, before anything is stored. It carries only the event itself — its <see cref="Kind"/> and <see cref="Phase"/>, when it occurred,
/// the organization, the actor and requester, the subject ids, and free-text <see cref="Detail"/>. Unlike the read
/// model <see cref="AccessAuditEvent"/>, it has no denormalized display fields (actor / requester / cipher /
/// collection / rule names); those are joined in on read. Emitted through the PAM audit-event emitter.
/// </summary>
public record AccessAuditEventData
{
    public required AccessAuditEventKind Kind { get; init; }

    /// <summary>Whether this is the pre-action attempt record or the post-action outcome record (before/after model).</summary>
    public AccessAuditEventPhase Phase { get; init; } = AccessAuditEventPhase.Outcome;

    /// <summary>
    /// Correlates an action's before/after pair: the Attempt and Outcome emitted from the same instance (via
    /// <c>with</c>) share this id, so the trail read can collapse them into one entry. Auto-generated per instance; a
    /// genuinely separate event emitted alongside (e.g. the automatic approval on an auto-approved submit) must be
    /// given its own id.
    /// </summary>
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    public required DateTime OccurredAt { get; init; }
    public required Guid OrganizationId { get; init; }

    /// <summary>Who performed the event; null for a system / automatic event (expiry, an automatic decision).</summary>
    public Guid? ActorId { get; init; }

    /// <summary>The owner of the subject request or lease (the access requester).</summary>
    public Guid? RequesterId { get; init; }

    public Guid? CollectionId { get; init; }
    public Guid? CipherId { get; init; }
    public Guid? AccessRequestId { get; init; }
    public Guid? AccessLeaseId { get; init; }
    public Guid? AccessRuleId { get; init; }

    /// <summary>
    /// The access rule's name, supplied by the rule commands (which hold the entity). Unlike the other display names,
    /// which are resolved by a JOIN at write time, the rule name is captured in C# because a rule can be hard-deleted
    /// in the same action, after which a JOIN could no longer resolve it. Null for non-rule events.
    /// </summary>
    public string? RuleName { get; init; }

    /// <summary>An approver comment, an auto-denial reason, or a revoke reason — whatever the action carried.</summary>
    public string? Detail { get; init; }

    /// <summary>The lease window, for lease-scoped events (activation, extension, end).</summary>
    public DateTime? LeaseNotBefore { get; init; }
    public DateTime? LeaseNotAfter { get; init; }
}

using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// The write-side payload for a PAM audit event: the raw facts a state-changing action records at the moment it
/// happens, before anything is stored. Unlike the read model <see cref="AccessAuditEvent"/> it carries no resolved
/// display names for the actor and requester; those are snapshotted into the row at write time. Emitted through the PAM
/// audit-event emitter.
/// </summary>
public record AccessAuditEventData
{
    public required AccessAuditEventKind Kind { get; init; }
    public AccessAuditEventPhase Phase { get; init; } = AccessAuditEventPhase.Outcome;

    /// <summary>
    /// Correlates an action's attempt/outcome pair: the Attempt and Outcome emitted from the same instance (via
    /// <c>with</c>) share this id, so the trail read can collapse them into one entry. A genuinely separate event
    /// emitted alongside (e.g. the automatic approval on an auto-approved submit) must be given its own id.
    /// </summary>
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    public required DateTime OccurredDate { get; init; }
    public required Guid OrganizationId { get; init; }
    public Guid? ActorId { get; init; }
    public Guid? RequesterId { get; init; }
    public Guid? CollectionId { get; init; }
    public Guid? CipherId { get; init; }
    public Guid? AccessRequestId { get; init; }
    public Guid? AccessLeaseId { get; init; }
    public Guid? AccessRuleId { get; init; }

    /// <summary>
    /// Supplied by the rule commands, which hold the entity, rather than resolved by a JOIN at write time: a rule can
    /// be hard-deleted in the same action, after which a JOIN could no longer resolve it. The target system and daemon
    /// names below follow the same pattern.
    /// </summary>
    public string? RuleName { get; init; }

    public Guid? TargetSystemId { get; init; }
    public string? TargetSystemName { get; init; }
    public Guid? DaemonId { get; init; }
    public string? DaemonName { get; init; }
    public Guid? RotationConfigId { get; init; }
    public Guid? RotationJobId { get; init; }
    public PamRotationSource? RotationSource { get; init; }
    public PamRotationSyncState? SyncState { get; init; }

    /// <summary>An approver comment, an auto-denial reason, or a revoke reason, whichever the action carried.</summary>
    public string? Detail { get; init; }

    public DateTime? LeaseNotBefore { get; init; }
    public DateTime? LeaseNotAfter { get; init; }
}

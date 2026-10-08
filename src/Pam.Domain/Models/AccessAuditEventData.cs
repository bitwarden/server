using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// The write-side payload of an audit event. Actor and requester display names are resolved and snapshotted at write
/// time, so it carries none.
/// </summary>
public record AccessAuditEventData
{
    public required AccessAuditEventKind Kind { get; init; }
    public AccessAuditEventPhase Phase { get; init; } = AccessAuditEventPhase.Outcome;

    /// <summary>
    /// Shared by an action's Attempt and Outcome when both are emitted from one instance via <c>with</c>. A separate
    /// event emitted alongside needs its own id.
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
    /// Supplied by the caller rather than joined at write time, since the same action can hard-delete the rule. The
    /// target system and access connector names work the same way.
    /// </summary>
    public string? RuleName { get; init; }

    public Guid? TargetSystemId { get; init; }
    public string? TargetSystemName { get; init; }
    public Guid? AccessConnectorId { get; init; }
    public string? AccessConnectorName { get; init; }
    public Guid? RotationConfigId { get; init; }
    public Guid? RotationJobId { get; init; }
    public PamRotationSource? RotationSource { get; init; }
    public PamRotationSyncState? SyncState { get; init; }

    public string? Detail { get; init; }

    public DateTime? LeaseNotBefore { get; init; }
    public DateTime? LeaseNotAfter { get; init; }
}

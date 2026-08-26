using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// One row in the PAM access-audit trail: the read model of a stored audit event (see
/// <see cref="AccessAuditEventData"/> for the write-side payload), with the display names that were snapshotted into
/// the row at write time. <see cref="Kind"/> carries the outcome, so no separate verdict field is needed.
/// <see cref="ActorId"/> is who performed the event (the approver on a decision, the revoker on a revoke, the requester
/// on a submission or self-end) and is null for a system or automatic event; <see cref="RequesterId"/> is the owner of
/// the subject request or lease. Subject ids are populated according to <see cref="Kind"/>.
/// </summary>
public class AccessAuditEvent
{
    /// <summary>The stored row's identifier. Pairs with <see cref="OccurredAt"/> to form the paging cursor.</summary>
    public Guid Id { get; set; }

    public AccessAuditEventKind Kind { get; set; }
    public AccessAuditEventPhase Phase { get; set; }

    /// <summary>Correlates an action's attempt/outcome pair; the trail read collapses events sharing this id into one entry.</summary>
    public Guid CorrelationId { get; set; }

    public DateTime OccurredAt { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ActorId { get; set; }
    public Guid? RequesterId { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? CipherId { get; set; }
    public Guid? AccessRequestId { get; set; }
    public Guid? AccessLeaseId { get; set; }
    public Guid? AccessRuleId { get; set; }
    public Guid? TargetSystemId { get; set; }
    public Guid? DaemonId { get; set; }
    public Guid? RotationConfigId { get; set; }
    public Guid? RotationJobId { get; set; }
    public PamRotationSource? RotationSource { get; set; }
    public PamRotationSyncState? SyncState { get; set; }

    /// <summary>An approver comment, an auto-denial reason, or a revoke reason, whichever the source row carried.</summary>
    public string? Detail { get; set; }

    public DateTime? LeaseNotBefore { get; set; }
    public DateTime? LeaseNotAfter { get; set; }

    // Display names frozen into the row at write time, all plaintext: the audit store holds no vault data, so the
    // subject cipher and collection are identified by id only. Any may be null when the referenced row is gone.
    public string? ActorName { get; set; }
    public string? ActorEmail { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
    public string? RuleName { get; set; }
    public string? TargetSystemName { get; set; }
    public string? DaemonName { get; set; }

    public bool Automated => ActorId is null;
}

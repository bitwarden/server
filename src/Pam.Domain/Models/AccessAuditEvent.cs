using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// The read model of a stored audit event. <see cref="Kind"/> carries the outcome, so there is no separate verdict
/// field. <see cref="ActorId"/> is who performed the event and is null for an automatic one; <see cref="RequesterId"/>
/// is the owner of the subject request or lease. Subject ids are populated according to <see cref="Kind"/>.
/// </summary>
public class AccessAuditEvent
{
    public Guid Id { get; set; }

    public AccessAuditEventKind Kind { get; set; }
    public AccessAuditEventPhase Phase { get; set; }

    /// <summary>Correlates an action's Attempt/Outcome pair; the trail read collapses them into one entry.</summary>
    public Guid CorrelationId { get; set; }

    public DateTime OccurredDate { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ActorId { get; set; }
    public Guid? RequesterId { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? CipherId { get; set; }
    public Guid? AccessRequestId { get; set; }
    public Guid? AccessLeaseId { get; set; }
    public Guid? AccessRuleId { get; set; }
    public Guid? TargetSystemId { get; set; }
    public Guid? AccessConnectorId { get; set; }
    public Guid? RotationConfigId { get; set; }
    public Guid? RotationJobId { get; set; }
    public PamRotationSource? RotationSource { get; set; }
    public PamRotationSyncState? SyncState { get; set; }

    /// <summary>An approver comment, an auto-denial reason, or a revoke reason, whichever the source row carried.</summary>
    public string? Detail { get; set; }

    public DateTime? LeaseNotBefore { get; set; }
    public DateTime? LeaseNotAfter { get; set; }

    // Frozen at write time. Any may be null when the referenced row is gone.
    public string? ActorName { get; set; }
    public string? ActorEmail { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
    public string? RuleName { get; set; }
    public string? TargetSystemName { get; set; }
    public string? AccessConnectorName { get; set; }

    public bool Automated => ActorId is null;
}

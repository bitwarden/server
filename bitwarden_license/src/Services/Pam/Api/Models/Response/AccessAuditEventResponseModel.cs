using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// One row of the PAM access-audit trail. Display names are as recorded when the event was written; subject ids are
/// populated according to <see cref="Kind"/>.
/// </summary>
public class AccessAuditEventResponseModel : ResponseModel
{
    public AccessAuditEventResponseModel(AccessAuditEvent auditEvent)
        : base("accessAuditEvent")
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        Kind = AccessAuditEventKindNames.From(auditEvent.Kind);
        OccurredAt = auditEvent.OccurredDate.AsUtc();
        OrganizationId = auditEvent.OrganizationId;
        ActorId = auditEvent.ActorId;
        RequesterId = auditEvent.RequesterId;
        CollectionId = auditEvent.CollectionId;
        CipherId = auditEvent.CipherId;
        RequestId = auditEvent.AccessRequestId;
        LeaseId = auditEvent.AccessLeaseId;
        RuleId = auditEvent.AccessRuleId;
        TargetSystemId = auditEvent.TargetSystemId;
        AccessConnectorId = auditEvent.AccessConnectorId;
        RotationConfigId = auditEvent.RotationConfigId;
        RotationJobId = auditEvent.RotationJobId;
        RotationSource = auditEvent.RotationSource;
        SyncState = auditEvent.SyncState;
        Detail = auditEvent.Detail;
        LeaseNotBefore = auditEvent.LeaseNotBefore.AsUtc();
        LeaseNotAfter = auditEvent.LeaseNotAfter.AsUtc();
        ActorName = auditEvent.ActorName;
        ActorEmail = auditEvent.ActorEmail;
        RequesterName = auditEvent.RequesterName;
        RequesterEmail = auditEvent.RequesterEmail;
        RuleName = auditEvent.RuleName;
        TargetSystemName = auditEvent.TargetSystemName;
        AccessConnectorName = auditEvent.AccessConnectorName;
        Automated = auditEvent.Automated;
        Incomplete = auditEvent.Phase == AccessAuditEventPhase.Attempt;
    }

    /// <summary>The event kind, as an <see cref="AccessAuditEventKindNames"/> name.</summary>
    public string Kind { get; }

    public DateTime OccurredAt { get; }
    public Guid OrganizationId { get; }

    /// <summary>Who performed the event; null for a system event.</summary>
    public Guid? ActorId { get; }

    /// <summary>The owner of the subject request or lease.</summary>
    public Guid? RequesterId { get; }

    public Guid? CollectionId { get; }
    public Guid? CipherId { get; }
    public Guid? RequestId { get; }
    public Guid? LeaseId { get; }
    public Guid? RuleId { get; }
    public Guid? TargetSystemId { get; }
    public Guid? AccessConnectorId { get; }
    public Guid? RotationConfigId { get; }
    public Guid? RotationJobId { get; }

    /// <summary>What triggered the rotation job; set on job and attempt events.</summary>
    public PamRotationSource? RotationSource { get; }

    /// <summary>Whether a failed attempt left the target system's password changed; set on failure events.</summary>
    public PamRotationSyncState? SyncState { get; }

    /// <summary>An approver comment or a revoke reason.</summary>
    public string? Detail { get; }

    public DateTime? LeaseNotBefore { get; }
    public DateTime? LeaseNotAfter { get; }

    /// <summary>The actor's display name and email. Null for a system event or an unresolved user.</summary>
    public string? ActorName { get; }
    public string? ActorEmail { get; }

    /// <summary>The requester's display name and email.</summary>
    public string? RequesterName { get; }
    public string? RequesterEmail { get; }

    /// <summary>The access rule's name, for rule administration events.</summary>
    public string? RuleName { get; }

    /// <summary>The target system's name, for rotation and target events.</summary>
    public string? TargetSystemName { get; }

    /// <summary>The access connector's name, for rotation and connector events.</summary>
    public string? AccessConnectorName { get; }

    /// <summary>True for a system event with no human actor.</summary>
    public bool Automated { get; }

    /// <summary>True when the action's outcome was never recorded, so it may not have completed.</summary>
    public bool Incomplete { get; }
}

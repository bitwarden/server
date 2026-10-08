using System.ComponentModel.DataAnnotations;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Pam.Enums;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// Derives from no domain entity, since the write payload and the read model differ in shape. There is no mapper
/// profile; the repository maps both directions.
/// </summary>
public class AccessAuditEvent
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CorrelationId { get; set; }
    public AccessAuditEventKind Kind { get; set; }
    public AccessAuditEventPhase Phase { get; set; }
    public DateTime OccurredDate { get; set; }

    // Not foreign keys, nor are the rotation ids below, so an audit event outlives what it references.
    public Guid? ActorId { get; set; }
    public Guid? RequesterId { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? CipherId { get; set; }
    public Guid? AccessRequestId { get; set; }
    public Guid? AccessLeaseId { get; set; }
    public Guid? AccessRuleId { get; set; }

    public string? Detail { get; set; }
    public DateTime? LeaseNotBefore { get; set; }
    public DateTime? LeaseNotAfter { get; set; }

    [MaxLength(50)]
    public string? ActorName { get; set; }

    [MaxLength(256)]
    public string? ActorEmail { get; set; }

    [MaxLength(50)]
    public string? RequesterName { get; set; }

    [MaxLength(256)]
    public string? RequesterEmail { get; set; }

    [MaxLength(256)]
    public string? RuleName { get; set; }

    public Guid? TargetSystemId { get; set; }

    [MaxLength(200)]
    public string? TargetSystemName { get; set; }

    public Guid? AccessConnectorId { get; set; }

    [MaxLength(200)]
    public string? AccessConnectorName { get; set; }

    public Guid? RotationConfigId { get; set; }
    public Guid? RotationJobId { get; set; }
    public PamRotationSource? RotationSource { get; set; }
    public PamRotationSyncState? SyncState { get; set; }

    public virtual Organization? Organization { get; set; }
}

using System.ComponentModel.DataAnnotations;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Pam.Enums;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The EF persistence model for one row of the PAM audit store. Unlike the other PAM models this doesn't derive
/// from a domain entity: the write payload and read model are deliberately different shapes and neither carries
/// an <c>Id</c>, so the stored row is its own type.
/// Mirrors [dbo].[AccessAuditEvent] (the event's facts plus names snapshotted at write time); there's no mapper
/// profile since the repository maps each direction explicitly.
/// </summary>
public class AccessAuditEvent
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CorrelationId { get; set; }
    public AccessAuditEventKind Kind { get; set; }
    public AccessAuditEventPhase Phase { get; set; }
    public DateTime OccurredAt { get; set; }

    // Subject ids are deliberately not foreign keys: an audit event outlives what it references.
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

    // Display names frozen at write time. Actor/requester name and email are plaintext; cipher and collection names
    // are encrypted (EncString) and decrypted client-side.
    [MaxLength(50)]
    public string? ActorName { get; set; }

    [MaxLength(256)]
    public string? ActorEmail { get; set; }

    [MaxLength(50)]
    public string? RequesterName { get; set; }

    [MaxLength(256)]
    public string? RequesterEmail { get; set; }

    public string? CipherName { get; set; }
    public string? CollectionName { get; set; }

    [MaxLength(256)]
    public string? RuleName { get; set; }

    // Rotation context; like the subject ids above, deliberately not foreign keyed, with names snapshotted so
    // the row still reads after the source rows are gone.
    public Guid? TargetSystemId { get; set; }

    [MaxLength(200)]
    public string? TargetSystemName { get; set; }

    public Guid? DaemonId { get; set; }

    [MaxLength(200)]
    public string? DaemonName { get; set; }

    public Guid? RotationConfigId { get; set; }
    public Guid? RotationJobId { get; set; }
    public PamRotationSource? RotationSource { get; set; }
    public PamRotationSyncState? SyncState { get; set; }

    public virtual Organization? Organization { get; set; }
}

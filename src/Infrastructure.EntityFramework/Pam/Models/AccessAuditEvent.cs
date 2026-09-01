using System.ComponentModel.DataAnnotations;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Pam.Enums;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Models;

/// <summary>
/// The EF persistence model for one row of the PAM audit store.
///
/// Unlike the other PAM models this does not derive from a domain entity: the store's write payload
/// (<see cref="Bit.Pam.Models.AccessAuditEventData"/>) and its read model (<see cref="Bit.Pam.Models.AccessAuditEvent"/>)
/// are deliberately different shapes and neither carries an <c>Id</c>, so the stored row is its own type. It mirrors
/// [dbo].[AccessAuditEvent]: the event's own facts, plus the display names snapshotted at write time so each row is
/// self-contained. There is no mapper profile — the repository maps both directions explicitly, because the write side
/// resolves the snapshot names and the read side does not.
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

    // Rotation context. Like the subject ids above these are deliberately not foreign keys, and the target and
    // daemon names are snapshotted so the row still reads once the rows they came from are gone.
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

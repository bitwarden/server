using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// One row in the PAM access-audit trail — the read model of a stored audit event (see <see cref="AccessAuditEventData"/>
/// for the write-side payload), with denormalized display fields joined on read. <see cref="Kind"/> carries the outcome, so no separate verdict field
/// is needed. <see cref="ActorId"/> is who performed the event (the approver on a decision, the revoker on a revoke,
/// the requester on a submission or self-end) and is null for a system / automatic event; <see cref="RequesterId"/> is
/// the owner of the subject request or lease. Subject ids are populated according to <see cref="Kind"/>.
/// </summary>
public class AccessAuditEvent
{
    public AccessAuditEventKind Kind { get; set; }

    /// <summary>Whether this row is the pre-action attempt or the post-action outcome (before/after model).</summary>
    public AccessAuditEventPhase Phase { get; set; }

    /// <summary>Correlates an action's before/after pair; the trail read collapses events sharing this id into one entry.</summary>
    public Guid CorrelationId { get; set; }

    public DateTime OccurredAt { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>Who performed the event; null for a system / automatic event (expiry, an automatic decision).</summary>
    public Guid? ActorId { get; set; }

    /// <summary>The owner of the subject request or lease (the access requester).</summary>
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

    /// <summary>What triggered the rotation job (scheduled, on-demand, or access-end); set on job/attempt-scoped events.</summary>
    public PamRotationSource? RotationSource { get; set; }

    /// <summary>Whether a failed attempt left the target system's password changed; set on failure/report events.</summary>
    public PamRotationSyncState? SyncState { get; set; }

    /// <summary>An approver comment, an auto-denial reason, or a revoke reason — whatever the source row carried.</summary>
    public string? Detail { get; set; }

    /// <summary>The lease window, for lease-scoped events (activation, extension, end).</summary>
    public DateTime? LeaseNotBefore { get; set; }
    public DateTime? LeaseNotAfter { get; set; }

    // Denormalized display fields joined by the projection. Actor/requester name and email are plaintext; cipher and
    // collection names are encrypted (the client decrypts them). Any may be null when the referenced row is gone.
    public string? ActorName { get; set; }
    public string? ActorEmail { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
    public string? CipherName { get; set; }
    public string? CollectionName { get; set; }

    /// <summary>The access rule's name — plaintext org configuration (not vault data), for rule administration events
    /// (created / updated / deleted). Null for non-rule events, or when the rule row is gone.</summary>
    public string? RuleName { get; set; }

    /// <summary>The target system's name — snapshotted at write by the rotation commands (same pattern as
    /// <see cref="RuleName"/>, not a read-time JOIN). Null for non-target events.</summary>
    public string? TargetSystemName { get; set; }

    /// <summary>The daemon's name — snapshotted at write by the rotation commands (same pattern as
    /// <see cref="RuleName"/>, not a read-time JOIN). Null for non-daemon events.</summary>
    public string? DaemonName { get; set; }

    /// <summary>True when there is no human actor — a system / automatic event. Drives the automated-vs-human filter.</summary>
    public bool Automated => ActorId is null;
}

using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// One row of the PAM access-audit trail, as the governance client renders it. Read from the dedicated audit store,
/// where each event was written self-contained (display names snapshotted at write time). <see cref="Kind"/> carries
/// the outcome (string vocabulary); <see cref="ActorId"/> is who performed it, null for a system / automatic event
/// (see <see cref="Automated"/>). Subject ids are populated according to the kind. <see cref="Detail"/> is an approver
/// comment or a revoke reason.
/// </summary>
public class AccessAuditEventResponseModel : ResponseModel
{
    public AccessAuditEventResponseModel(AccessAuditEvent auditEvent)
        : base("accessAuditEvent")
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        Kind = AccessAuditEventKindNames.From(auditEvent.Kind);
        OccurredAt = auditEvent.OccurredAt.AsUtc();
        OrganizationId = auditEvent.OrganizationId;
        ActorId = auditEvent.ActorId;
        RequesterId = auditEvent.RequesterId;
        CollectionId = auditEvent.CollectionId;
        CipherId = auditEvent.CipherId;
        RequestId = auditEvent.AccessRequestId;
        LeaseId = auditEvent.AccessLeaseId;
        RuleId = auditEvent.AccessRuleId;
        TargetSystemId = auditEvent.TargetSystemId;
        DaemonId = auditEvent.DaemonId;
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
        CipherName = auditEvent.CipherName;
        CollectionName = auditEvent.CollectionName;
        RuleName = auditEvent.RuleName;
        TargetSystemName = auditEvent.TargetSystemName;
        DaemonName = auditEvent.DaemonName;
        Automated = auditEvent.Automated;
        Incomplete = auditEvent.Phase == AccessAuditEventPhase.Attempt;
    }

    /// <summary>The event kind, as the governance vocabulary (see <see cref="AccessAuditEventKindNames"/>).</summary>
    public string Kind { get; }

    public DateTime OccurredAt { get; }
    public Guid OrganizationId { get; }

    /// <summary>Who performed the event; null for a system / automatic event.</summary>
    public Guid? ActorId { get; }

    /// <summary>The owner of the subject request or lease.</summary>
    public Guid? RequesterId { get; }

    public Guid? CollectionId { get; }
    public Guid? CipherId { get; }
    public Guid? RequestId { get; }
    public Guid? LeaseId { get; }
    public Guid? RuleId { get; }
    public Guid? TargetSystemId { get; }
    public Guid? DaemonId { get; }
    public Guid? RotationConfigId { get; }
    public Guid? RotationJobId { get; }

    /// <summary>What triggered the rotation job (scheduled, on-demand, or access-end); set on job/attempt-scoped events.</summary>
    public PamRotationSource? RotationSource { get; }

    /// <summary>Whether a failed attempt left the target system's password changed; set on failure/report events.</summary>
    public PamRotationSyncState? SyncState { get; }

    /// <summary>An approver comment or a revoke reason, if the source carried one.</summary>
    public string? Detail { get; }

    public DateTime? LeaseNotBefore { get; }
    public DateTime? LeaseNotAfter { get; }

    /// <summary>The actor's display name and email (plaintext). Null for a system event or an unresolved user.</summary>
    public string? ActorName { get; }
    public string? ActorEmail { get; }

    /// <summary>The requester's display name and email (plaintext).</summary>
    public string? RequesterName { get; }
    public string? RequesterEmail { get; }

    /// <summary>The cipher and collection names — encrypted; the client decrypts them.</summary>
    public string? CipherName { get; }
    public string? CollectionName { get; }

    /// <summary>The access rule's name — plaintext org configuration (not vault data), for rule administration events.</summary>
    public string? RuleName { get; }

    /// <summary>The target system's name — plaintext org configuration, snapshotted at write, for rotation/target events.</summary>
    public string? TargetSystemName { get; }

    /// <summary>The daemon's name — plaintext org configuration, snapshotted at write, for rotation/daemon events.</summary>
    public string? DaemonName { get; }

    /// <summary>True when there is no human actor — a system / automatic event.</summary>
    public bool Automated { get; }

    /// <summary>
    /// True when this row is an action whose outcome never landed — only the write-ahead Attempt was recorded, so it is
    /// in-doubt (may have failed or been interrupted). False for a normal completed action.
    /// </summary>
    public bool Incomplete { get; }
}

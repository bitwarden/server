using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Pam.Entities;

/// <summary>
/// The rotation setup for a single vault cipher (invariant <c>OneConfigPerCipher</c> — a cipher has at most one
/// config): which <see cref="PamTargetSystem"/> it rotates against, the account it rotates, and when it is next due.
/// A null <see cref="ScheduleCron"/> means the config never rotates on a schedule (on-demand and/or access-end only,
/// or — on a manual target — awaiting a human to record a rotation).
/// </summary>
public class PamRotationConfig : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>The cipher whose credential this config rotates. Unique across configs (<c>OneConfigPerCipher</c>).</summary>
    public Guid CipherId { get; set; }

    public Guid TargetSystemId { get; set; }

    /// <summary>
    /// The account this config rotates on the target system. Opaque to the server — never parsed; only the daemon
    /// interprets it.
    /// </summary>
    [MaxLength(500)]
    public string AccountIdentity { get; set; } = null!;

    /// <summary>
    /// Whether a successful rotation should also terminate the account's existing sessions on the target. May only
    /// be true when the target is automatic and reports <see cref="PamTargetSystem.SupportsSessionTermination"/>.
    /// </summary>
    public bool TerminateSessions { get; set; }

    /// <summary>Quartz 6-field cron expression. Null means no scheduled rotation for this config.</summary>
    [MaxLength(100)]
    public string? ScheduleCron { get; set; }

    /// <summary>Whether a lease ending on this config's cipher should trigger a rotation (spec <c>RotateOnAccessEnd</c>).</summary>
    public bool RotateOnAccessEnd { get; set; }

    /// <summary>
    /// When this config is next due. On an automatic target, the sweep offers a job once this is reached (spec
    /// <c>RotationDue</c>). On a manual target, reaching this instead marks the config
    /// <c>awaiting_manual_rotation</c> — there is no job, only an operator obligation. Null means nothing is due.
    /// </summary>
    public DateTime? NextRotationAt { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>When the last rotation for this config completed successfully. Null until the first success.</summary>
    public DateTime? LastRotationAt { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}

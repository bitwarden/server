using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Pam.Entities;

/// <summary>The rotation setup for one vault cipher (<c>OneConfigPerCipher</c>).</summary>
public class PamRotationConfig : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    public Guid CipherId { get; set; }

    public Guid TargetSystemId { get; set; }

    /// <summary>
    /// The target-system account to rotate. Opaque to the server; only the access connector interprets it.
    /// </summary>
    [MaxLength(500)]
    public string AccountIdentity { get; set; } = null!;

    /// <summary>
    /// Only valid on an automatic target that reports <see cref="PamTargetSystem.SupportsSessionTermination"/>.
    /// </summary>
    public bool TerminateSessions { get; set; }

    /// <summary>A Quartz 6-field cron expression; null means no scheduled rotation.</summary>
    [MaxLength(100)]
    public string? ScheduleCron { get; set; }

    public bool RotateOnAccessEnd { get; set; }

    /// <summary>
    /// Once reached, an automatic target gets a job offered and a manual one awaits a human rotation. Null means
    /// nothing is due.
    /// </summary>
    public DateTime? NextRotationAt { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTime? LastRotationAt { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}

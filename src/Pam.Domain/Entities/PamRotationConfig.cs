using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Pam.Entities;

/// <summary>The rotation setup for one vault cipher (<c>OneConfigPerCipher</c>).</summary>
public class PamRotationConfig : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    public Guid CipherId { get; set; } // Also sealed into the blob, as proof the admin configured it.

    public Guid TargetSystemId { get; set; } // Also sealed into the blob, as proof the admin configured it.

    /// <summary>
    /// An opaque blob the admin client seals under the org key (spec <c>RotationConfigSealedV1</c>): the account
    /// identity to rotate and whether to terminate sessions. The server never parses it.
    /// </summary>
    [MaxLength(10000)]
    public string SealedData { get; set; } = null!;

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

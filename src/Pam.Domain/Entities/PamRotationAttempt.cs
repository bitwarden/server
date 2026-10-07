using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>One access connector's try at a <see cref="PamRotationJob"/>, inserted atomically with its claim.</summary>
public class PamRotationAttempt : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    /// <summary>Never cleared, unlike the job's claim fields.</summary>
    public Guid ClaimedByAccessConnectorId { get; set; }

    /// <summary>Set once the rotated secret is written to the cipher; a success report requires it.</summary>
    public bool CipherUpdated { get; set; }

    /// <summary>
    /// What the claimant reported, if anything. Executing vs Abandoned is derived via
    /// <see cref="PamRotationStatusDerivation.ComputeAttemptStatus"/>.
    /// </summary>
    public PamRotationAttemptAction Action { get; set; }

    /// <summary>
    /// Set only on <see cref="PamRotationAttemptStatus.Errored"/>, truncated to 500 characters. Never raw
    /// target-system output, which can echo credentials.
    /// </summary>
    [MaxLength(500)]
    public string? FailureReason { get; set; }

    /// <summary>Set only on Errored.</summary>
    public PamRotationSyncState? SyncState { get; set; }

    /// <summary>Set only on Rotated.</summary>
    public PamSessionTerminationOutcome? SessionTermination { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>When the claimant reported, or when a release ended the claim. A timeout writes nothing, so a timed-out
    /// attempt has none stored.</summary>
    public DateTime? ResolvedDate { get; set; }

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}

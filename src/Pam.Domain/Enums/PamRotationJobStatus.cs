namespace Bit.Pam.Enums;

/// <summary>
/// Where a <see cref="Entities.PamRotationJob"/> stands, derived at read time by
/// <see cref="PamRotationStatusDerivation.ComputeJobStatus"/>. <see cref="Pending"/> and <see cref="Claimed"/> are
/// active (<c>PamRotationRules.IsActiveJobStatus</c>); the rest are terminal.
/// </summary>
public enum PamRotationJobStatus : byte
{
    /// <summary>Offered, or returned to claimable after a retry or a release.</summary>
    Pending = 0,

    Claimed = 1,

    Succeeded = 2,

    /// <summary>The retry budget (<c>MaxAttempts</c>) is exhausted.</summary>
    Failed = 3,

    /// <summary>Neither succeeded nor failed by <see cref="Entities.PamRotationJob.ExpiresAt"/>.</summary>
    TimedOut = 4,
}

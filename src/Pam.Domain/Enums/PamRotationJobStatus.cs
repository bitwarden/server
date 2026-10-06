namespace Bit.Pam.Enums;

/// <summary>
/// Where a <see cref="Entities.PamRotationJob"/> stands, derived at read time by
/// <see cref="PamRotationStatusDerivation.ComputeJobStatus"/> and never stored. <see cref="Pending"/> and
/// <see cref="Claimed"/> are the active statuses (see <c>PamRotationRules.IsActiveJobStatus</c>); every other value is
/// terminal.
/// </summary>
public enum PamRotationJobStatus : byte
{
    /// <summary>Offered and claimable, or returned to claimable after a retry or a release.</summary>
    Pending = 0,

    /// <summary>Held by <see cref="Entities.PamRotationJob.ClaimedByAccessConnectorId"/> until it succeeds, is
    /// released, retried, or times out.</summary>
    Claimed = 1,

    /// <summary>An attempt against this job reported success.</summary>
    Succeeded = 2,

    /// <summary>Every attempt errored and the retry budget (<c>MaxAttempts</c>) is exhausted.</summary>
    Failed = 3,

    /// <summary>Neither succeeded nor failed by <see cref="Entities.PamRotationJob.ExpiresAt"/>.</summary>
    TimedOut = 4,
}

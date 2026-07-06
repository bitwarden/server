namespace Bit.Pam.Enums;

/// <summary>
/// Lifecycle of a <see cref="Entities.PamRotationJob"/>. <see cref="Pending"/> and <see cref="Claimed"/> are the
/// active statuses a config's invariant <c>AtMostOneActiveJobPerConfig</c> binds on (see
/// <c>PamRotationRules.IsActiveJobStatus</c>); every other value is terminal.
/// </summary>
public enum PamRotationJobStatus : byte
{
    /// <summary>Offered and claimable, or returned to claimable after a retry or a release.</summary>
    Pending = 0,

    /// <summary>Held by <see cref="Entities.PamRotationJob.ClaimedByDaemonId"/> until it succeeds, is released, retried, or times out.</summary>
    Claimed = 1,

    /// <summary>An attempt against this job reported success.</summary>
    Succeeded = 2,

    /// <summary>Every attempt errored and the retry budget (<c>MaxAttempts</c>) is exhausted.</summary>
    Failed = 3,

    /// <summary>Still Pending or Claimed past <see cref="Entities.PamRotationJob.ExpiresAt"/> with no successful attempt.</summary>
    TimedOut = 4,
}

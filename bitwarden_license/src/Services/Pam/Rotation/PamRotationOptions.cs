namespace Bit.Services.Pam.Rotation;

/// <summary>
/// Timing knobs for PAM credential rotation, bound from <c>globalSettings:pam:rotation</c> (see
/// <c>AddPamServices</c>). Every consumer injects <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> of
/// this type rather than reading configuration directly, so the defaults below are the single source of truth for
/// an unconfigured environment.
/// </summary>
public class PamRotationOptions
{
    /// <summary>How long a rotation job may live before the sweep times it out (spec <c>JobTimesOut</c>).</summary>
    public TimeSpan JobTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>The number of Errored attempts a job may accrue before it fails outright.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>The base of the exponential retry backoff: <c>RetryBaseDelay * 2^(erroredCount-1)</c>.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>The claim lease length: how long a claiming daemon has before the release sweep may reclaim its job.</summary>
    public TimeSpan ReleaseDelay { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How far out a config's next rotation is pushed after its job fails outright (budget exhausted).</summary>
    public TimeSpan FailureRetryDelay { get; set; } = TimeSpan.FromHours(1);

    /// <summary>How long since its last heartbeat a daemon is still considered connected (spec <c>DaemonConnection</c>).</summary>
    public TimeSpan DaemonOfflineAfter { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>The minimum gap between conditional heartbeat writes, so a polling daemon does not hammer its row.</summary>
    public TimeSpan HeartbeatMinInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>The minimum gap the schedule calculator enforces between two consecutive occurrences of a config's cron.</summary>
    public TimeSpan MinScheduleInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>The minimum gap between two on-demand triggers of the same config (abuse floor).</summary>
    public TimeSpan OnDemandCooldown { get; set; } = TimeSpan.FromMinutes(1);
}

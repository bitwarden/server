namespace Bit.Services.Pam.AccessConnector;

/// <summary>
/// Timing settings for PAM credential rotation, bound from <c>globalSettings:pam:rotation</c>. The defaults below
/// apply to an unconfigured environment.
/// </summary>
public class PamRotationOptions
{
    /// <summary>How long a rotation job may live before the sweep times it out (spec <c>JobTimesOut</c>).</summary>
    public TimeSpan JobTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>The number of Errored attempts a job may accrue before it fails outright.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>The base of the exponential retry backoff: <c>RetryBaseDelay * 2^(erroredCount-1)</c>.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The claim lease length. Past it, the release sweep may reclaim a job whose access connector has gone offline.
    /// </summary>
    public TimeSpan ReleaseDelay { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How far a scheduled config's next rotation is pushed out after its job fails or times out.</summary>
    public TimeSpan FailureRetryDelay { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// How long after its last heartbeat an access connector still counts as connected (spec
    /// <c>ConnectorConnection</c>).
    /// </summary>
    public TimeSpan AccessConnectorOfflineAfter { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Minimum gap between heartbeat writes, so a polling access connector does not hammer its row.</summary>
    public TimeSpan HeartbeatMinInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Minimum gap between two consecutive occurrences of a config's cron.</summary>
    public TimeSpan MinScheduleInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Minimum gap between a config's last rotation and an on-demand trigger (abuse floor).</summary>
    public TimeSpan OnDemandCooldown { get; set; } = TimeSpan.FromMinutes(1);
}

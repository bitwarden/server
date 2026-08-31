using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Pam;

/// <summary>
/// Derived predicates over PAM rotation entities, implemented once so admin commands, the daemon-facing endpoints,
/// and the sweep jobs cannot drift on a guard's definition (mirrors the Allium spec's own predicate refactor).
/// </summary>
public static class PamRotationRules
{
    /// <summary>
    /// Spec <c>DaemonConnection</c>: a daemon is connected when it has heartbeated within <paramref name="offlineAfter"/>
    /// of <paramref name="now"/>. A daemon that has never heartbeated is never connected.
    /// </summary>
    public static bool IsConnected(PamDaemon daemon, DateTime now, TimeSpan offlineAfter) =>
        daemon.LastHeartbeatAt is { } lastHeartbeatAt && lastHeartbeatAt >= now - offlineAfter;

    /// <summary>
    /// The "active" job statuses invariant <c>AtMostOneActiveJobPerConfig</c> binds on: a job is active while it is
    /// still claimable or being worked.
    /// </summary>
    public static bool IsActiveJobStatus(PamRotationJobStatus status) =>
        status is PamRotationJobStatus.Pending or PamRotationJobStatus.Claimed;

    /// <summary>
    /// Spec <c>can_offer</c>, minus the has-active-job check — callers combine this with a repository lookup, since
    /// that check needs a query this pure predicate can't make. The config must be enabled, on an
    /// <see cref="PamTargetSystemMethod.Automatic"/> target, and that target must be
    /// <see cref="PamTargetSystemStatus.Active"/>.
    /// </summary>
    public static bool CanOffer(PamRotationConfig config, PamTargetSystemMethod method, PamTargetSystemStatus targetStatus) =>
        config.Enabled && method == PamTargetSystemMethod.Automatic && targetStatus == PamTargetSystemStatus.Active;

    /// <summary>
    /// Spec <c>awaiting_manual_rotation</c>: a manual-target config surfaces an operator obligation once its
    /// schedule comes due, since there is no daemon to offer a job to.
    /// </summary>
    public static bool AwaitingManualRotation(PamRotationConfig config, PamTargetSystemMethod method, DateTime now) =>
        method == PamTargetSystemMethod.Manual && config.Enabled
        && config.NextRotationAt is { } nextRotationAt && nextRotationAt <= now;

    /// <summary>
    /// The claim's lease deadline — <see cref="PamRotationJob.ClaimedAt"/> plus <paramref name="releaseDelay"/> —
    /// the point at which the release sweep may reclaim the job from a daemon whose heartbeat has gone stale. Null
    /// unless the job is currently claimed.
    /// </summary>
    public static DateTime? ExecuteBy(PamRotationJob job, TimeSpan releaseDelay) =>
        job.ClaimedAt is { } claimedAt ? claimedAt + releaseDelay : null;
}

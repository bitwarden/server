namespace Bit.Services.Pam.Rotation.Jobs;

/// <summary>
/// Runs the three time-derived rotation sweeps (spec <c>RotationDue</c>, <c>JobTimesOut</c>,
/// <c>DaemonConnectionDropsReleaseJobs</c>): offering due scheduled configs, timing out expired jobs, and releasing
/// jobs whose claiming daemon has gone stale past its claim lease. Invoked on a Quartz cron by
/// <see cref="PamRotationSweepJob"/>; kept separate from the job class so the sweep logic itself is testable without
/// a <c>Quartz.IJobExecutionContext</c>.
/// </summary>
public interface IPamRotationSweepService
{
    /// <summary>
    /// Runs all three phases in sequence. Each phase is independently fault-isolated: an exception in one (or in one
    /// row within one) is logged and swallowed rather than propagated, so a failure in an earlier phase never
    /// prevents the later phases -- or later rows in the same phase -- from running.
    /// </summary>
    Task SweepAsync();
}

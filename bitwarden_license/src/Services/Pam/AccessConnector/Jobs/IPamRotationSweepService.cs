namespace Bit.Services.Pam.AccessConnector.Jobs;

/// <summary>
/// Runs the three time-derived rotation sweeps: offering due scheduled configs, timing out expired jobs, and
/// releasing jobs whose claiming access connector has gone stale.
/// </summary>
public interface IPamRotationSweepService
{
    /// <summary>
    /// Runs the phases in sequence. An exception in one phase, or on one row, is logged and swallowed, so later
    /// phases and rows still run.
    /// </summary>
    Task SweepAsync();
}

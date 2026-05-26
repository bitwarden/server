using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;
using Spectre.Console;

namespace Bit.SeederUtility.Helpers;

/// <summary>
/// Renders <see cref="SeederProgressEvent"/> into a Spectre.Console <see cref="ProgressContext"/>.
/// One <see cref="ProgressTask"/> per phase name.
/// </summary>
/// <remarks>
/// Thread-safe: the task dictionary is guarded by a lock because <see cref="PhaseAdvanced"/> events
/// can arrive from <see cref="Parallel.For"/> worker threads. <see cref="ProgressTask.Increment"/>
/// itself is safe to call concurrently per Spectre.Console's documentation.
/// </remarks>
internal sealed class ConsoleProgressReporter(ProgressContext ctx) : IProgress<SeederProgressEvent>
{
    private readonly Dictionary<string, ProgressTask> _tasks = new();
    private readonly object _lock = new();

    public void Report(SeederProgressEvent value)
    {
        switch (value)
        {
            case PhaseStarted started:
                lock (_lock)
                {
                    if (_tasks.ContainsKey(started.Phase))
                    {
                        return;
                    }

                    var task = ctx.AddTask(started.Phase, maxValue: started.Total ?? 1d);
                    if (started.Total is null)
                    {
                        task.IsIndeterminate = true;
                    }
                    _tasks[started.Phase] = task;
                }
                break;

            case PhaseAdvanced advanced:
                ProgressTask? advanceTask;
                lock (_lock)
                {
                    _tasks.TryGetValue(advanced.Phase, out advanceTask);
                }
                advanceTask?.Increment(advanced.Delta);
                break;

            case PhaseCompleted completed:
                ProgressTask? completeTask;
                lock (_lock)
                {
                    _tasks.TryGetValue(completed.Phase, out completeTask);
                }
                if (completeTask is not null)
                {
                    completeTask.IsIndeterminate = false;
                    completeTask.Value = completeTask.MaxValue;
                    completeTask.StopTask();
                }
                break;
        }
    }

    /// <summary>
    /// Runs <paramref name="seed"/> inside a Spectre progress context, wiring a reporter
    /// into <paramref name="deps"/>. The seeder's emitted events drive the live bars.
    /// </summary>
    /// <remarks>
    /// Progress output is written to stderr so stdout remains clean for downstream consumers
    /// that pipe the final summary rows (org ID, counts, etc.) into other tools.
    /// </remarks>
    internal static TResult RunWithProgress<TResult>(
        SeederDependencies deps,
        Func<SeederDependencies, TResult> seed)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(Console.Error),
        });

        TResult result = default!;
        console.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .Start(ctx =>
            {
                var reporter = new ConsoleProgressReporter(ctx);
                result = seed(deps with { Progress = reporter });
            });
        return result;
    }
}

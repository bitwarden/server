namespace Bit.Seeder.Pipeline;

/// <summary>
/// Batches <see cref="PhaseAdvanced"/> events so a sequential loop over N items emits at most ~100 events.
/// Caller is responsible for calling <see cref="PhaseStarted"/>/<see cref="PhaseCompleted"/>; this
/// type only handles per-iteration flushing. Not thread-safe — parallel loops should use thread-local
/// counters and report directly via <see cref="IProgress{T}.Report"/>.
/// </summary>
internal sealed class ProgressTicker
{
    private readonly IProgress<SeederProgressEvent>? _progress;
    private readonly string _phase;
    private readonly int _batchSize;
    private int _pending;

    internal ProgressTicker(IProgress<SeederProgressEvent>? progress, string phase, int total)
    {
        _progress = progress;
        _phase = phase;
        _batchSize = Math.Max(1, total / 100);
    }

    internal void Tick()
    {
        if (_progress is null)
        {
            return;
        }

        _pending++;
        if (_pending >= _batchSize)
        {
            _progress.Report(new PhaseAdvanced(_phase, _pending));
            _pending = 0;
        }
    }

    internal void Flush()
    {
        if (_progress is null || _pending <= 0)
        {
            return;
        }

        _progress.Report(new PhaseAdvanced(_phase, _pending));
        _pending = 0;
    }
}

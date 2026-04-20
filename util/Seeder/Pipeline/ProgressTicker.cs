namespace Bit.Seeder.Pipeline;

/// <summary>
/// Batches <see cref="PhaseAdvanced"/> events so a loop over N items emits at most ~100 events.
/// Caller is responsible for calling <see cref="PhaseStarted"/>/<see cref="PhaseCompleted"/>; this
/// type only handles per-iteration flushing.
/// </summary>
/// <remarks>
/// For sequential loops, call <see cref="Tick"/> once per iteration.
/// For parallel loops, maintain a thread-local counter and call <see cref="TickBy"/> when it crosses the batch size.
/// Always call <see cref="Flush"/> at the end to drain any remainder.
/// When <see cref="IProgress{T}"/> is null, all methods are no-ops with no allocations beyond the ticker itself.
/// </remarks>
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

    /// <summary>Accumulates <paramref name="delta"/> and flushes when the batch threshold is reached.</summary>
    internal void TickBy(int delta)
    {
        if (_progress is null || delta <= 0)
        {
            return;
        }

        _pending += delta;
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

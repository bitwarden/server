namespace Bit.Seeder.Pipeline;

/// <summary>
/// Wraps an <see cref="IStep"/> or <see cref="IAsyncStep"/> with an order index and a
/// post-commit flag so the executor can sort and partition steps registered as keyed
/// services (where <c>GetKeyedServices</c> does not guarantee order).
/// </summary>
/// <remarks>
/// Implements <see cref="IAsyncStep"/> so the executor can dispatch every step through
/// a single async entry point regardless of whether the underlying step is sync or async.
/// </remarks>
internal sealed class OrderedStep : IAsyncStep
{
    private readonly IStep? _syncStep;
    private readonly IAsyncStep? _asyncStep;

    internal int Order { get; }

    internal bool IsPostCommit { get; }

    internal object Inner => (object?)_syncStep ?? _asyncStep!;

    internal OrderedStep(IStep step, int order, bool isPostCommit = false)
    {
        _syncStep = step;
        Order = order;
        IsPostCommit = isPostCommit;
    }

    internal OrderedStep(IAsyncStep step, int order, bool isPostCommit = false)
    {
        _asyncStep = step;
        Order = order;
        IsPostCommit = isPostCommit;
    }

    public Task ExecuteAsync(SeederContext context)
    {
        if (_syncStep is not null)
        {
            _syncStep.Execute(context);
            return Task.CompletedTask;
        }
        return _asyncStep!.ExecuteAsync(context);
    }
}

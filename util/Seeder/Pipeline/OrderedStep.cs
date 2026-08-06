namespace Bit.Seeder.Pipeline;

/// <summary>
/// Wraps an <see cref="IStep"/> or <see cref="IAsyncStep"/> with an order index and a post-commit
/// flag for keyed DI registration, where GetKeyedServices does not guarantee order.
/// </summary>
/// <remarks>
/// Deliberately does not implement <see cref="IAsyncStep"/>: nothing resolves that interface from DI,
/// and implementing it would let an <see cref="OrderedStep"/> be passed back to
/// <c>RecipeBuilder.AddAsyncStep</c> and silently double-wrapped.
/// </remarks>
internal sealed class OrderedStep
{
    private readonly IStep? _sync;

    private readonly IAsyncStep? _async;

    // Guarded so exactly one arm is always populated: RecipeBuilder.AddStep/AddAsyncStep are public
    // and take a caller-supplied factory, so a null return would otherwise leave both fields null and
    // surface as a NullReferenceException from the *async* arm of ExecuteAsync.
    internal OrderedStep(IStep step, int order, bool isPostCommit)
    {
        ArgumentNullException.ThrowIfNull(step);
        _sync = step;
        Order = order;
        IsPostCommit = isPostCommit;
    }

    internal OrderedStep(IAsyncStep step, int order, bool isPostCommit)
    {
        ArgumentNullException.ThrowIfNull(step);
        _async = step;
        Order = order;
        IsPostCommit = isPostCommit;
    }

    internal int Order { get; }

    internal bool IsPostCommit { get; }

    /// <summary>
    /// The wrapped step. Typed as <see cref="object"/> because <see cref="IStep"/> and
    /// <see cref="IAsyncStep"/> share no base type; exposed only so tests can assert registration
    /// order by concrete step type.
    /// </summary>
    internal object Inner => _sync ?? (object)_async!;

    /// <summary>
    /// Runs the wrapped step. Synchronous steps run inline on the calling thread and return an
    /// already-completed task, so awaiting this never yields for them.
    /// </summary>
    internal Task ExecuteAsync(SeederContext context)
    {
        if (_sync is not null)
        {
            _sync.Execute(context);
            return Task.CompletedTask;
        }

        return _async!.ExecuteAsync(context);
    }
}

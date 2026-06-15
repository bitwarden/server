namespace Bit.Core.Platform.Data;

public sealed class RootTransactionScope : ITransactionScope
{
    private readonly TransactionHolder _holder;
    private bool _disposed;

    public RootTransactionScope(TransactionHolder holder)
    {
        _holder = holder;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_holder.Doomed)
        {
            throw new InvalidOperationException(
                "Cannot commit a transaction that has been marked for rollback by a nested scope.");
        }

        await _holder.Transaction.CommitAsync(cancellationToken);
        _holder.MarkCommitted();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        _holder.MarkDoomed();
        await _holder.Transaction.RollbackAsync(cancellationToken);
        _holder.MarkRolledBack();
    }

    // Intentionally NOT `async`. AsyncLocal mutations inside an async state machine
    // never flow back to the caller - see TransactionManagerBase for the same trick.
    // Clearing TransactionState.Current must happen on the caller's stack frame so the
    // slot is null once the `await using` block exits.
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return default;
        }

        _disposed = true;
        TransactionState.Current = null;
        return _holder.DisposeAsync();
    }
}

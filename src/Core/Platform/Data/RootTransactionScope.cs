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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        TransactionState.Current = null;
        await _holder.DisposeAsync();
    }
}

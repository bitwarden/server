namespace Bit.Core.Platform.Data;

public sealed class NestedTransactionScope : ITransactionScope
{
    private readonly TransactionHolder _holder;
    private bool _disposed;

    public NestedTransactionScope(TransactionHolder holder)
    {
        _holder = holder;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        // Nested scope commit is a no-op; only the root scope commits.
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        // Mark the transaction as doomed so the root scope cannot commit.
        _holder.Doomed = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _holder.ReferenceCount--;
        return ValueTask.CompletedTask;
    }
}

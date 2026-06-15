using System.Data;

namespace Bit.Core.Platform.Data;

public abstract class TransactionManagerBase : ITransactionManager
{
    public Task<ITransactionScope> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var existing = TransactionState.Current;
        if (existing is not null)
        {
            return Task.FromResult<ITransactionScope>(new NestedTransactionScope(existing));
        }

        var holder = new TransactionHolder();
        TransactionState.Current = holder;

        return InitializeRootScopeAsync(holder, isolationLevel, cancellationToken);
    }

    private async Task<ITransactionScope> InitializeRootScopeAsync(
        TransactionHolder holder,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        try
        {
            await InitializeRootHolderAsync(holder, isolationLevel, cancellationToken);
        }
        catch
        {
            // The holder reference is still pinned in the caller's ExecutionContext
            // (we can't clear it from inside an async method), but disposing the
            // (possibly half-open) resources prevents a leak. Subsequent calls in the
            // caller's flow will see a populated-but-dead holder until they open a
            // new flow; this is acceptable for an init-time failure.
            await holder.DisposeAsync();
            throw;
        }

        return new RootTransactionScope(holder);
    }

    /// <summary>
    /// Populates the pre-allocated holder with the provider's open connection and
    /// transaction. Implementations must call <see cref="TransactionHolder.Initialize"/>
    /// on the holder.
    /// </summary>
    protected abstract Task InitializeRootHolderAsync(
        TransactionHolder holder,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken);
}

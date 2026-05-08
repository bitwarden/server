using System.Data;

namespace Bit.Core.Platform.Data;

public abstract class TransactionManagerBase : ITransactionManager
{
    public bool HasActiveTransaction => TransactionState.Current is not null;

    public async Task<ITransactionScope> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var existing = TransactionState.Current;
        if (existing is not null)
        {
            return new NestedTransactionScope(existing);
        }

        var holder = await CreateRootHolderAsync(isolationLevel, cancellationToken);
        TransactionState.Current = holder;
        return new RootTransactionScope(holder);
    }

    protected abstract Task<TransactionHolder> CreateRootHolderAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken);
}

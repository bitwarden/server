using System.Data.Common;

namespace Bit.Core.Platform.Data;

public static class TransactionState
{
    private static readonly AsyncLocal<TransactionHolder?> _current = new();

    public static TransactionHolder? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}

public sealed class TransactionHolder : IAsyncDisposable
{
    public required DbConnection Connection { get; init; }
    public required DbTransaction Transaction { get; init; }

    /// <summary>
    /// True when this holder is responsible for disposing <see cref="Connection"/>.
    /// EF reuses the DbContext's connection and must leave its lifetime to the scope;
    /// Dapper opens its own connection and must dispose it here.
    /// </summary>
    public bool OwnsConnection { get; init; } = true;

    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }
    public bool Doomed { get; private set; }

    /// <summary>For EF: the DatabaseContext associated with this transaction.</summary>
    public object? DbContext { get; private set; }

    /// <summary>For EF: the IServiceScope that owns the DatabaseContext.</summary>
    private IAsyncDisposable? _scope;

    /// <summary>One-way latch: records that the root scope committed.</summary>
    public void MarkCommitted() => Committed = true;

    /// <summary>One-way latch: records that the root scope rolled back.</summary>
    public void MarkRolledBack() => RolledBack = true;

    /// <summary>
    /// One-way latch: marks the transaction as doomed. Once doomed, the root scope
    /// cannot commit.
    /// </summary>
    public void MarkDoomed() => Doomed = true;

    /// <summary>
    /// Attaches an EF DbContext (and the scope that owns it) to this holder. May be
    /// called at most once per holder; subsequent calls throw.
    /// </summary>
    public void AttachDbContext(object dbContext, IAsyncDisposable scope)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(scope);

        if (DbContext is not null)
        {
            throw new InvalidOperationException(
                "A DbContext is already attached to this transaction.");
        }

        DbContext = dbContext;
        _scope = scope;
    }

    public async ValueTask DisposeAsync()
    {
        if (!Committed && !RolledBack)
        {
            try
            {
                await Transaction.RollbackAsync();
            }
            catch
            {
                // Best-effort rollback; connection may already be broken
            }
        }

        await Transaction.DisposeAsync();

        if (OwnsConnection)
        {
            await Connection.DisposeAsync();
        }

        if (_scope is not null)
        {
            await _scope.DisposeAsync();
        }
    }
}

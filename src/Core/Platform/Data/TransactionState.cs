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
    private DbConnection? _connection;
    private DbTransaction? _transaction;
    private IAsyncDisposable? _scope;

    /// <summary>
    /// The open connection backing this transaction. Available only after
    /// <see cref="Initialize"/> has been called.
    /// </summary>
    public DbConnection Connection =>
        _connection ?? throw new InvalidOperationException(
            "Transaction holder has not been initialized. Call Initialize first.");

    /// <summary>
    /// The open transaction. Available only after <see cref="Initialize"/> has been called.
    /// </summary>
    public DbTransaction Transaction =>
        _transaction ?? throw new InvalidOperationException(
            "Transaction holder has not been initialized. Call Initialize first.");

    /// <summary>
    /// True when this holder is responsible for disposing <see cref="Connection"/>.
    /// EF reuses the DbContext's connection and must leave its lifetime to the scope;
    /// Dapper opens its own connection and must dispose it here.
    /// </summary>
    public bool OwnsConnection { get; private set; } = true;

    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }
    public bool Doomed { get; private set; }

    /// <summary>For EF: the DatabaseContext associated with this transaction.</summary>
    public object? DbContext { get; private set; }

    /// <summary>
    /// Populates the holder with its open connection and transaction. May be called at most
    /// once per holder; the manager allocates the holder synchronously before any await so
    /// the <see cref="TransactionState.Current"/> reference flows into the caller's
    /// ExecutionContext, then awaits the I/O that fills in these fields.
    /// </summary>
    public void Initialize(DbConnection connection, DbTransaction transaction, bool ownsConnection = true)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        if (_connection is not null)
        {
            throw new InvalidOperationException("Transaction holder is already initialized.");
        }

        _connection = connection;
        _transaction = transaction;
        OwnsConnection = ownsConnection;
    }

    public void MarkCommitted() => Committed = true;

    public void MarkRolledBack() => RolledBack = true;

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
        if (_transaction is not null && !Committed && !RolledBack)
        {
            try
            {
                await _transaction.RollbackAsync();
            }
            catch
            {
                // Best-effort rollback; connection may already be broken.
            }
        }

        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }

        if (OwnsConnection && _connection is not null)
        {
            await _connection.DisposeAsync();
        }

        if (_scope is not null)
        {
            await _scope.DisposeAsync();
        }
    }
}

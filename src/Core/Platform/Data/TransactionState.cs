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
    public bool Committed { get; set; }
    public bool RolledBack { get; set; }
    public bool Doomed { get; set; }

    /// <summary>
    /// True when this holder is responsible for disposing <see cref="Connection"/>.
    /// EF reuses the DbContext's connection and must leave its lifetime to the scope;
    /// Dapper opens its own connection and must dispose it here.
    /// </summary>
    public bool OwnsConnection { get; init; } = true;

    /// <summary>
    /// For EF: the DatabaseContext associated with this transaction.
    /// </summary>
    public object? DbContext { get; set; }

    /// <summary>
    /// For EF: the IServiceScope that owns the DatabaseContext.
    /// </summary>
    public IAsyncDisposable? Scope { get; set; }

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

        if (Scope is not null)
        {
            await Scope.DisposeAsync();
        }
    }
}

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
    public int ReferenceCount { get; set; } = 1;
    public bool Committed { get; set; }
    public bool Doomed { get; set; }

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
        if (!Committed)
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
        await Connection.DisposeAsync();

        if (Scope is not null)
        {
            await Scope.DisposeAsync();
        }
    }
}

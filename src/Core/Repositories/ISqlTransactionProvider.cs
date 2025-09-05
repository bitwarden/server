using System.Data;
using System.Data.Common;

namespace Bit.Core.Repositories;

public interface ISqlTransactionProvider
{
    Task<DbTransaction> GetTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
}

// public interface ITransactionScope : IAsyncDisposable
// {
//     DbTransaction Transaction { get; }
//     Task CommitAsync();
//     Task RollbackAsync();
//     bool IsActive { get; }
// }
//
// public abstract class TransactionScopeBase : ITransactionScope
// {
//     protected bool _isCommitted;
//     protected bool _isDisposed;
//     public abstract DbTransaction Transaction { get; }
//     public abstract DbConnection Connection { get; }
//     public bool IsActive => !_isCommitted && !_isDisposed;
//
//     public abstract Task CommitAsync();
//     public abstract Task RollbackAsync();
//     public abstract ValueTask DisposeAsync();
// }

public static class TransactionRetryPolicy
{
    public static async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        delay ??= TimeSpan.FromSeconds(1);

        var attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsRetryableException(ex) && attempt < maxRetries - 1)
            {
                attempt++;
                await Task.Delay(delay.Value * attempt);
            }
        }

        throw new InvalidOperationException($"Operation failed after {maxRetries} attempts.");
    }

    private static bool IsRetryableException(Exception ex) =>
        ex.Message.Contains("deadlock") ||
        ex.Message.Contains("timeout") ||
        ex.Message.Contains("transaction") ||
        ex is InvalidOperationException;
}

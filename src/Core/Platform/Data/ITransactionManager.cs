using System.Data;

namespace Bit.Core.Platform.Data;

/// <summary>
/// Manages ambient database transactions that span multiple repository calls.
/// Implementations are singleton-safe; transaction state is stored per async flow.
/// </summary>
public interface ITransactionManager
{
    /// <summary>
    /// Begins a new ambient transaction. All repository operations on the current
    /// async flow will use the same connection and transaction until disposed.
    /// Supports nesting: inner calls increment a reference count; only the
    /// outermost Dispose/Commit actually affects the database. The isolation level
    /// on a nested call is ignored — the inner scope joins the outer transaction.
    /// </summary>
    Task<ITransactionScope> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the current async flow has an active ambient transaction.
    /// </summary>
    bool HasActiveTransaction { get; }
}

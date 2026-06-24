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
    /// Supports nesting: inner calls return a no-op scope that shares the outer
    /// transaction. Only the outermost Commit/Dispose actually affects the database.
    /// The isolation level on nested calls is ignored — inner scopes inherit the
    /// outer transaction's isolation level.
    /// </summary>
    Task<ITransactionScope> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
}

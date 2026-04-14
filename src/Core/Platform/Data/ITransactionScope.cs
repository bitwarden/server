namespace Bit.Core.Platform.Data;

/// <summary>
/// Represents an ambient transaction scope. Commit must be called explicitly;
/// disposing without committing triggers rollback.
/// </summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

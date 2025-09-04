using System.Data.Common;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using IsolationLevel = System.Data.IsolationLevel;

namespace Bit.Infrastructure.EntityFramework;

public class EntityFrameworkTransactionProvider(IServiceScopeFactory serviceScopeFactory) : ISqlTransactionProvider
{
    public async Task<DbTransaction> GetTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        return (await dbContext.Database.BeginTransactionAsync()).GetDbTransaction();
    }
}

// public class EntityFrameworkTransactionScope : TransactionScopeBase
// {
//     private readonly IDbContextTransaction _contextTransaction;
//     private readonly IServiceScope _serviceScope;
//     private readonly DatabaseContext _dbContext;
//
//     public EntityFrameworkTransactionScope(
//         IDbContextTransaction contextTransaction,
//         IServiceScope serviceScope,
//         DatabaseContext dbContext)
//     {
//         _contextTransaction = contextTransaction;
//         _serviceScope = serviceScope;
//         _dbContext = dbContext;
//         Transaction = contextTransaction.GetDbTransaction();
//         Connection = Transaction.Connection!;
//
//     }
//
//     public override DbTransaction Transaction { get; }
//     public override DbConnection Connection { get; }
//
//     public override async Task CommitAsync()
//     {
//         if (_isCommitted || _isDisposed)
//         {
//             throw new InvalidOperationException("Transaction is already committed or disposed");
//         }
//
//         await _contextTransaction.CommitAsync();
//         _isCommitted = true;
//     }
//
//     public override async Task RollbackAsync()
//     {
//         if (_isDisposed)
//             throw new InvalidOperationException("Transaction is already disposed");
//
//         if (!_isCommitted)
//             await _contextTransaction.RollbackAsync();
//     }
//
//     public override async ValueTask DisposeAsync()
//     {
//         if (_isDisposed) return;
//
//         if (!_isCommitted)
//         {
//             try
//             {
//                 await _contextTransaction.RollbackAsync();
//             }
//             catch
//             {
//                 // Log but don't throw during disposal
//             }
//         }
//
//         await _contextTransaction.DisposeAsync();
//         _serviceScope.Dispose();
//         _isDisposed = true;
//     }
//
// }

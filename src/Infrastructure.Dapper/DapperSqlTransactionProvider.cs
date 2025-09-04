using System.Data;
using System.Data.Common;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper;

public class DapperSqlTransactionProvider(GlobalSettings settings) : ISqlTransactionProvider
{
    public async Task<DbTransaction> GetTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        return await new SqlConnection(settings.SqlServer.ConnectionString).BeginTransactionAsync(isolationLevel);
    }
}
//
// public class DapperTransactionScope : TransactionScopeBase
// {
//     private readonly DbConnection _connection;
//     private readonly DbTransaction _transaction;
//
//     public DapperTransactionScope(DbConnection connection, DbTransaction transaction)
//     {
//         _connection = connection;
//         _transaction = transaction;
//     }
//
//     public override DbTransaction Transaction => _transaction;
//     public override DbConnection Connection => _connection;
//
//     public override async Task CommitAsync()
//     {
//         if (_isCommitted || _isDisposed)
//             throw new InvalidOperationException("Transaction is already committed or disposed");
//
//         await _transaction.CommitAsync();
//         _isCommitted = true;
//     }
//
//     public override async Task RollbackAsync()
//     {
//         if (_isDisposed)
//             throw new InvalidOperationException("Transaction is already disposed");
//
//         if (!_isCommitted)
//             await _transaction.RollbackAsync();
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
//                 await _transaction.RollbackAsync();
//             }
//             catch
//             {
//                 // Log but don't throw during disposal
//             }
//         }
//
//         await _transaction.DisposeAsync();
//         await _connection.DisposeAsync();
//         _isDisposed = true;
//     }
// }

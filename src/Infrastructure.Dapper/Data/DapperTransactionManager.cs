using System.Data.Common;
using Bit.Core.Platform.Data;
using Bit.Core.Settings;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Data;

public sealed class DapperTransactionManager : ITransactionManager
{
    private readonly string _connectionString;

    public DapperTransactionManager(GlobalSettings globalSettings)
    {
        _connectionString = globalSettings.SqlServer.ConnectionString;
    }

    public bool HasActiveTransaction => TransactionState.Current is not null;

    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var existing = TransactionState.Current;
        if (existing is not null)
        {
            existing.ReferenceCount++;
            return new NestedTransactionScope(existing);
        }

        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var transaction = (DbTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var holder = new TransactionHolder
        {
            Connection = connection,
            Transaction = transaction,
        };
        TransactionState.Current = holder;
        return new RootTransactionScope(holder);
    }
}

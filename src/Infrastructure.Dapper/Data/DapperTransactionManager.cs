using System.Data;
using Bit.Core.Platform.Data;
using Bit.Core.Settings;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Data;

public sealed class DapperTransactionManager(GlobalSettings globalSettings) : TransactionManagerBase
{
    private readonly string _connectionString = globalSettings.SqlServer.ConnectionString;

    protected override async Task<TransactionHolder> CreateRootHolderAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);

        return new TransactionHolder
        {
            Connection = connection,
            Transaction = transaction,
        };
    }
}

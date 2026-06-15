using System.Data;
using Bit.Core.Platform.Data;
using Bit.Core.Settings;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Data;

public sealed class DapperTransactionManager(GlobalSettings globalSettings) : TransactionManagerBase
{
    private readonly string _connectionString = globalSettings.SqlServer.ConnectionString;

    protected override async Task InitializeRootHolderAsync(
        TransactionHolder holder,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);

        holder.Initialize(connection, transaction);
    }
}

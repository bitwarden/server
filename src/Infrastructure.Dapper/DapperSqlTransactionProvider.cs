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

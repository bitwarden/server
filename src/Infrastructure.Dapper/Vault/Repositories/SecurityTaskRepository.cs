using System.Data;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Vault.Repositories;

public class SecurityTaskRepository : Repository<SecurityTask, Guid>, ISecurityTaskRepository
{
    public SecurityTaskRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public SecurityTaskRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    {
    }

    /// <inheritdoc />
    public async Task<ICollection<SecurityTask>> GetManyByUserIdStatusAsync(Guid userId,
        IEnumerable<SecurityTaskStatus> status = null)
    {
        var statusTable = new DataTable();
        statusTable.Columns.Add("Vaule", typeof(byte));

        if (status != null)
        {
            foreach (var s in status)
            {
                statusTable.Rows.Add(s);
            }
        }

        await using var connection = new SqlConnection(ConnectionString);

        var results = await connection.QueryAsync<SecurityTask>(
            $"[{Schema}].[SecurityTask_ReadByUserIdStatus]",
            new {UserId = userId, Statuses = statusTable.AsTableValuedParameter("dbo.SecurityTaskStatusArray")},
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }
}

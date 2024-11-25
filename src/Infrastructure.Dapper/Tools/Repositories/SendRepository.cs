#nullable enable

using System.Data;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.Dapper.Repositories;
using Bit.Infrastructure.Dapper.Tools.Helpers;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Tools.Repositories;

/// <inheritdoc cref="ISendRepository" />
public class SendRepository : Repository<Send, Guid>, ISendRepository
{
    public SendRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public SendRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    /// <inheritdoc />
    public async Task<ICollection<Send>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Send>(
                $"[{Schema}].[Send_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Send>> GetManyByDeletionDateAsync(DateTime deletionDateBefore)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Send>(
                $"[{Schema}].[Send_ReadByDeletionDateBefore]",
                new { DeletionDate = deletionDateBefore },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    /// <inheritdoc />
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid userId, IEnumerable<Send> sends)
    {
        return async (connection, transaction) =>
        {
            // Create temp table
            var sqlCreateTemp = @"
                            SELECT TOP 0 *
                            INTO #TempSend
                            FROM [dbo].[Send]";

            await using (var cmd = new SqlCommand(sqlCreateTemp, connection, transaction))
            {
                cmd.ExecuteNonQuery();
            }

            // Bulk copy data into temp table
            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
            {
                bulkCopy.DestinationTableName = "#TempSend";
                var sendsTable = sends.ToDataTable();
                foreach (DataColumn col in sendsTable.Columns)
                {
                    bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                }

                sendsTable.PrimaryKey = new DataColumn[] { sendsTable.Columns[0] };
                await bulkCopy.WriteToServerAsync(sendsTable);
            }

            // Update send table from temp table
            var sql = @"
                UPDATE
                    [dbo].[Send]
                SET
                    [Key] = TS.[Key],
                    [RevisionDate] = TS.[RevisionDate]
                FROM
                    [dbo].[Send] S
                INNER JOIN
                    #TempSend TS ON S.Id = TS.Id
                WHERE
                    S.[UserId] = @UserId
                DROP TABLE #TempSend";

            await using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
                cmd.ExecuteNonQuery();
            }
        };
    }
}

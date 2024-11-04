using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Auth.Helpers;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Auth.Repositories;

public class EmergencyAccessRepository : Repository<EmergencyAccess, Guid>, IEmergencyAccessRepository
{
    public EmergencyAccessRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public EmergencyAccessRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<int> GetCountByGrantorIdEmailAsync(Guid grantorId, string email, bool onlyRegisteredUsers)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteScalarAsync<int>(
                "[dbo].[EmergencyAccess_ReadCountByGrantorIdEmail]",
                new { GrantorId = grantorId, Email = email, OnlyUsers = onlyRegisteredUsers },
                commandType: CommandType.StoredProcedure);

            return results;
        }
    }

    public async Task<ICollection<EmergencyAccessDetails>> GetManyDetailsByGrantorIdAsync(Guid grantorId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<EmergencyAccessDetails>(
                "[dbo].[EmergencyAccessDetails_ReadByGrantorId]",
                new { GrantorId = grantorId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<EmergencyAccessDetails>> GetManyDetailsByGranteeIdAsync(Guid granteeId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<EmergencyAccessDetails>(
                "[dbo].[EmergencyAccessDetails_ReadByGranteeId]",
                new { GranteeId = granteeId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<EmergencyAccessDetails?> GetDetailsByIdGrantorIdAsync(Guid id, Guid grantorId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<EmergencyAccessDetails>(
                "[dbo].[EmergencyAccessDetails_ReadByIdGrantorId]",
                new { Id = id, GrantorId = grantorId },
                commandType: CommandType.StoredProcedure);

            return results.FirstOrDefault();
        }
    }

    public async Task<ICollection<EmergencyAccessNotify>> GetManyToNotifyAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<EmergencyAccessNotify>(
                "[dbo].[EmergencyAccess_ReadToNotify]",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<EmergencyAccessDetails>> GetExpiredRecoveriesAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<EmergencyAccessDetails>(
                "[dbo].[EmergencyAccessDetails_ReadExpiredRecoveries]",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    /// <inheritdoc />
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(
        Guid grantorId, IEnumerable<EmergencyAccess> emergencyAccessKeys)
    {
        return async (SqlConnection connection, SqlTransaction transaction) =>
        {
            // Create temp table
            var sqlCreateTemp = @"
                            SELECT TOP 0 *
                            INTO #TempEmergencyAccess
                            FROM [dbo].[EmergencyAccess]";

            await using (var cmd = new SqlCommand(sqlCreateTemp, connection, transaction))
            {
                cmd.ExecuteNonQuery();
            }

            // Bulk copy data into temp table
            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
            {
                bulkCopy.DestinationTableName = "#TempEmergencyAccess";
                var emergencyAccessTable = emergencyAccessKeys.ToDataTable();
                foreach (DataColumn col in emergencyAccessTable.Columns)
                {
                    bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                }

                emergencyAccessTable.PrimaryKey = new DataColumn[] { emergencyAccessTable.Columns[0] };
                await bulkCopy.WriteToServerAsync(emergencyAccessTable);
            }

            // Update emergency access table from temp table
            var sql = @"
                UPDATE
                    [dbo].[EmergencyAccess]
                SET
                    [KeyEncrypted] = TE.[KeyEncrypted]
                FROM
                    [dbo].[EmergencyAccess] E
                INNER JOIN
                    #TempEmergencyAccess TE ON E.Id = TE.Id
                WHERE
                    E.[GrantorId] = @GrantorId

                DROP TABLE #TempEmergencyAccess";

            await using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                cmd.Parameters.Add("@GrantorId", SqlDbType.UniqueIdentifier).Value = grantorId;
                cmd.ExecuteNonQuery();
            }
        };
    }
}

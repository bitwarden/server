using System.Data;
using Bit.Core;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.Dapper.Repositories;
using Bit.Infrastructure.Dapper.Tools.Helpers;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Tools.Repositories;

/// <inheritdoc cref="IReceiveRepository" />
public class ReceiveRepository : Repository<Receive, Guid>, IReceiveRepository
{
    private readonly IDataProtector _dataProtector;

    public ReceiveRepository(GlobalSettings globalSettings, IDataProtectionProvider dataProtectionProvider)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString, dataProtectionProvider)
    { }

    public ReceiveRepository(string connectionString, string readOnlyConnectionString, IDataProtectionProvider dataProtectionProvider)
        : base(connectionString, readOnlyConnectionString)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
    }

    public override async Task<Receive?> GetByIdAsync(Guid id)
    {
        var receive = await base.GetByIdAsync(id);
        UnprotectData(receive);
        return receive;
    }

    /// <inheritdoc />
    public async Task<ICollection<Receive>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Receive>(
                $"[{Schema}].[Receive_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            var receives = results.ToList();
            UnprotectData(receives);
            return receives;
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Receive>> GetManyByExpirationDateAsync(DateTime expirationDateBefore)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Receive>(
                $"[{Schema}].[Receive_ReadByExpirationDateBefore]",
                new { ExpirationDate = expirationDateBefore },
                commandType: CommandType.StoredProcedure);

            var receives = results.ToList();
            UnprotectData(receives);
            return receives;
        }
    }

    public override async Task<Receive> CreateAsync(Receive receive)
    {
        await ProtectDataAndSaveAsync(receive, async () => await base.CreateAsync(receive));
        return receive;
    }

    public override async Task ReplaceAsync(Receive receive)
    {
        await ProtectDataAndSaveAsync(receive, async () => await base.ReplaceAsync(receive));
    }

    /// <inheritdoc />
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid userId, IEnumerable<Receive> receives)
    {
        return async (connection, transaction) =>
        {
            // Protect all receives before bulk update
            var receivesList = receives.ToList();
            foreach (var receive in receivesList)
            {
                ProtectData(receive);
            }

            // Create temp table
            var sqlCreateTemp = @"
                            SELECT TOP 0 *
                            INTO #TempReceive
                            FROM [dbo].[Receive]";

            await using (var cmd = new SqlCommand(sqlCreateTemp, connection, transaction))
            {
                cmd.ExecuteNonQuery();
            }

            // Bulk copy data into temp table
            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
            {
                bulkCopy.DestinationTableName = "#TempReceive";
                var receivesTable = receivesList.ToDataTable();
                foreach (DataColumn col in receivesTable.Columns)
                {
                    bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                }

                receivesTable.PrimaryKey = new DataColumn[] { receivesTable.Columns[0] };
                await bulkCopy.WriteToServerAsync(receivesTable);
            }

            // Update receive table from temp table
            var sql = @"
                UPDATE
                    [dbo].[Receive]
                SET
                    [UserKeyWrappedSharedContentEncryptionKey] = TR.[UserKeyWrappedSharedContentEncryptionKey],
                    [UserKeyWrappedPrivateKey] = TR.[UserKeyWrappedPrivateKey],
                    [RevisionDate] = TR.[RevisionDate]
                FROM
                    [dbo].[Receive] R
                INNER JOIN
                    #TempReceive TR ON R.Id = TR.Id
                WHERE
                    R.[UserId] = @UserId
                DROP TABLE #TempReceive";

            await using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
                cmd.ExecuteNonQuery();
            }

            // Unprotect after save
            foreach (var receive in receivesList)
            {
                UnprotectData(receive);
            }
        };
    }

    private async Task ProtectDataAndSaveAsync(Receive receive, Func<Task> saveTask)
    {
        var secret = receive.Secret;
        ProtectData(receive);
        await saveTask();
        receive.Secret = secret;
    }

    private void ProtectData(Receive receive)
    {
        if (!receive.Secret.StartsWith(Constants.DatabaseFieldProtectedPrefix))
        {
            receive.Secret = string.Concat(Constants.DatabaseFieldProtectedPrefix,
                _dataProtector.Protect(receive.Secret));
        }
    }

    private void UnprotectData(Receive? receive)
    {
        if (receive == null)
        {
            return;
        }

        if (receive.Secret.StartsWith(Constants.DatabaseFieldProtectedPrefix))
        {
            receive.Secret = _dataProtector.Unprotect(
                receive.Secret.Substring(Constants.DatabaseFieldProtectedPrefix.Length));
        }
    }

    private void UnprotectData(IEnumerable<Receive> receives)
    {
        foreach (var receive in receives)
        {
            UnprotectData(receive);
        }
    }
}

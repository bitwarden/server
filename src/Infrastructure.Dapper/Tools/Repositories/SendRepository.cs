#nullable enable

using System.Data;
using System.Security.Cryptography;
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
using Microsoft.Extensions.Logging;

namespace Bit.Infrastructure.Dapper.Tools.Repositories;

/// <inheritdoc cref="ISendRepository" />
public class SendRepository : Repository<Send, Guid>, ISendRepository
{
    private readonly IDataProtector _dataProtector;
    private readonly ILogger<SendRepository> _logger;

    public SendRepository(GlobalSettings globalSettings, IDataProtectionProvider dataProtectionProvider, ILogger<SendRepository> logger)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString, dataProtectionProvider, logger)
    { }

    public SendRepository(string connectionString, string readOnlyConnectionString, IDataProtectionProvider dataProtectionProvider, ILogger<SendRepository> logger)
        : base(connectionString, readOnlyConnectionString)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
        _logger = logger;
    }

    public override async Task<Send?> GetByIdAsync(Guid id)
    {
        var send = await base.GetByIdAsync(id);
        if (send == null)
        {
            return null;
        }
        return UnprotectData(send) ? send : null;
    }

    /// <inheritdoc />
    public async Task<ICollection<Send>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Send>(
                $"[{Schema}].[Send_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.Where(UnprotectData).ToList();
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Send>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Send>(
                $"[{Schema}].[Send_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.Where(UnprotectData).ToList();
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Send>> GetManyFileSendsByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Send>(
                $"[{Schema}].[Send_ReadFilesByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.Where(UnprotectData).ToList();
        }
    }

    /// <inheritdoc />
    public async Task<ICollection<Send>> GetManyFileSendsByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Send>(
                $"[{Schema}].[Send_ReadFilesByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.Where(UnprotectData).ToList();
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

            // Don't filter or decrypt here — the cleanup job needs to see every row
            // (including unrecoverable ones) so it can delete them.
            return results.ToList();
        }
    }

    public override async Task<Send> CreateAsync(Send send)
    {
        await ProtectDataAndSaveAsync(send, async () => await base.CreateAsync(send));
        return send;
    }

    public override async Task ReplaceAsync(Send send)
    {
        await ProtectDataAndSaveAsync(send, async () => await base.ReplaceAsync(send));
    }

    /// <inheritdoc />
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid userId, IEnumerable<Send> sends)
    {
        return async (connection, transaction) =>
        {
            // Protect all sends before bulk update
            var sendsList = sends.ToList();
            foreach (var send in sendsList)
            {
                ProtectData(send);
            }

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
                var sendsTable = sendsList.ToDataTable();
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

            // Restore in-memory Emails. The DB write only touched Key/RevisionDate, so
            // a per-row decryption failure here is benign — discard the bool return.
            foreach (var send in sendsList)
            {
                _ = UnprotectData(send);
            }
        };
    }

    private async Task ProtectDataAndSaveAsync(Send send, Func<Task> saveTask)
    {
        if (send == null)
        {
            await saveTask();
            return;
        }

        // Capture original value
        var emails = send.Emails;

        // Protect value
        ProtectData(send);

        // Save
        await saveTask();

        // Restore original value
        send.Emails = emails;
    }

    private void ProtectData(Send send)
    {
        if (send.Emails == null || send.Emails.StartsWith(Constants.DatabaseFieldProtectedPrefix))
        {
            return;
        }

        send.Emails = string.Concat(Constants.DatabaseFieldProtectedPrefix,
            _dataProtector.Protect(send.Emails));
    }

    private bool UnprotectData(Send send)
    {
        if (send.Emails == null || !send.Emails.StartsWith(Constants.DatabaseFieldProtectedPrefix))
        {
            return true;
        }

        try
        {
            send.Emails = _dataProtector.Unprotect(
                send.Emails.Substring(Constants.DatabaseFieldProtectedPrefix.Length));
            return true;
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Failed to unprotect Emails for Send {SendId}.", send.Id);
            return false;
        }
    }
}

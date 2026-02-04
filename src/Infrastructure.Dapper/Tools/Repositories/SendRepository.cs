#nullable enable

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

/// <inheritdoc cref="ISendRepository" />
public class SendRepository : Repository<Send, Guid>, ISendRepository
{
    private readonly IDataProtector _dataProtector;

    public SendRepository(GlobalSettings globalSettings, IDataProtectionProvider dataProtectionProvider)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString, dataProtectionProvider)
    { }

    public SendRepository(string connectionString, string readOnlyConnectionString, IDataProtectionProvider dataProtectionProvider)
        : base(connectionString, readOnlyConnectionString)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
    }

    public override async Task<Send?> GetByIdAsync(Guid id)
    {
        var send = await base.GetByIdAsync(id);
        UnprotectData(send);
        return send;
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

            var sends = results.ToList();
            UnprotectData(sends);
            return sends;
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

            var sends = results.ToList();
            UnprotectData(sends);
            return sends;
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

            // Unprotect after save
            foreach (var send in sendsList)
            {
                UnprotectData(send);
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
        var anonAccessEmails = send.AnonAccessEmails;

        // Protect value
        ProtectData(send);

        // Save
        await saveTask();

        // Restore original value
        send.AnonAccessEmails = anonAccessEmails;
    }

    private void ProtectData(Send send)
    {
        if (!send.AnonAccessEmails?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? false)
        {
            send.AnonAccessEmails = string.Concat(Constants.DatabaseFieldProtectedPrefix,
                _dataProtector.Protect(send.AnonAccessEmails!));
        }
    }

    private void UnprotectData(Send? send)
    {
        if (send == null)
        {
            return;
        }

        if (send.AnonAccessEmails?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? false)
        {
            send.AnonAccessEmails = _dataProtector.Unprotect(
                send.AnonAccessEmails.Substring(Constants.DatabaseFieldProtectedPrefix.Length));
        }
    }

    private void UnprotectData(IEnumerable<Send> sends)
    {
        if (sends == null)
        {
            return;
        }

        foreach (var send in sends)
        {
            UnprotectData(send);
        }
    }
}

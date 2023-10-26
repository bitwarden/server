using System.Data;
using Bit.Core.Tools.Entities;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Tools.Helpers;

public static class SendHelpers
{

    public static DataTable ToTVP(this IEnumerable<Send> sends)
    {
        var sendsTable = new DataTable();
        sendsTable.SetTypeName("[dbo].[Send]");

        var columnData = new List<(string name, Type type, Func<Send, object> getter)>
        {
            (nameof(Send.Id), typeof(Guid), c => c.Id),
            (nameof(Send.UserId), typeof(Guid), c => c.UserId),
            (nameof(Send.OrganizationId), typeof(Guid), c => c.OrganizationId),
            (nameof(Send.Type), typeof(short), c => c.Type),
            (nameof(Send.Data), typeof(string), c => c.Data),
            (nameof(Send.Key), typeof(string), c => c.Key),
            (nameof(Send.Password), typeof(string), c => c.Password),
            (nameof(Send.MaxAccessCount), typeof(int), c => c.MaxAccessCount),
            (nameof(Send.AccessCount), typeof(int), c => c.AccessCount),
            (nameof(Send.CreationDate), typeof(DateTime), c => c.CreationDate),
            (nameof(Send.RevisionDate), typeof(DateTime), c => c.RevisionDate),
            (nameof(Send.ExpirationDate), typeof(DateTime), c => c.ExpirationDate),
            (nameof(Send.DeletionDate), typeof(DateTime), c => c.DeletionDate),
            (nameof(Send.Disabled), typeof(bool), c => c.Disabled),
            (nameof(Send.HideEmail), typeof(bool), c => c.HideEmail),
        };

        return sends.BuildTable(sendsTable, columnData);
    }

    public static void UpdateEncryptedData(this IEnumerable<Send> sends, Guid userId, SqlConnection connection, SqlTransaction transaction)
    {
        // Create temp table
        var sqlCreateTemp = @"
                            SELECT TOP 0 *
                            INTO #TempSend
                            FROM [dbo].[Send]";

        using (var cmd = new SqlCommand(sqlCreateTemp, connection, transaction))
        {
            cmd.ExecuteNonQuery();
        }

        // Bulk copy data into temp table
        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
        {
            bulkCopy.DestinationTableName = "#TempSend";
            var sendsTable = sends.ToTVP();
            foreach (DataColumn col in sendsTable.Columns)
            {
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }

            sendsTable.PrimaryKey = new DataColumn[] { sendsTable.Columns[0] };
            bulkCopy.WriteToServer(sendsTable);
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

        using (var cmd = new SqlCommand(sql, connection, transaction))
        {
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.ExecuteNonQuery();
        }
    }
}

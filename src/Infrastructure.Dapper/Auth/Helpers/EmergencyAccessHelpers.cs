using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Infrastructure.Dapper;
using Microsoft.Data.SqlClient;

public static class EmergencyAccessHelpers
{
    public static DataTable ToDataTable(this IEnumerable<EmergencyAccess> emergencyAccesses)
    {
        var emergencyAccessTable = new DataTable();

        var columnData = new List<(string name, Type type, Func<EmergencyAccess, object> getter)>
        {
            (nameof(EmergencyAccess.Id), typeof(Guid), c => c.Id),
            (nameof(EmergencyAccess.GrantorId), typeof(Guid), c => c.GrantorId),
            (nameof(EmergencyAccess.GranteeId), typeof(Guid), c => c.GranteeId),
            (nameof(EmergencyAccess.Email), typeof(string), c => c.Email),
            (nameof(EmergencyAccess.KeyEncrypted), typeof(string), c => c.KeyEncrypted),
            (nameof(EmergencyAccess.WaitTimeDays), typeof(int), c => c.WaitTimeDays),
            (nameof(EmergencyAccess.Type), typeof(short), c => c.Type),
            (nameof(EmergencyAccess.Status), typeof(short), c => c.Status),
            (nameof(EmergencyAccess.RecoveryInitiatedDate), typeof(DateTime), c => c.RecoveryInitiatedDate),
            (nameof(EmergencyAccess.LastNotificationDate), typeof(DateTime), c => c.LastNotificationDate),
            (nameof(EmergencyAccess.CreationDate), typeof(DateTime), c => c.CreationDate),
            (nameof(EmergencyAccess.RevisionDate), typeof(DateTime), c => c.RevisionDate),
        };

        return emergencyAccesses.BuildTable(emergencyAccessTable, columnData);
    }

    public static void UpdateEncryptedData(this IEnumerable<EmergencyAccess> emergencyAccesses, Guid userId, SqlConnection connection, SqlTransaction transaction)
    {
        // Create temp table
        var sqlCreateTemp = @"
                            SELECT TOP 0 *
                            INTO #TempEmergencyAccess
                            FROM [dbo].[EmergencyAccess]";

        using (var cmd = new SqlCommand(sqlCreateTemp, connection, transaction))
        {
            cmd.ExecuteNonQuery();
        }

        // Bulk copy data into temp table
        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
        {
            bulkCopy.DestinationTableName = "#TempEmergencyAccess";
            var emergencyAccessTable = emergencyAccesses.ToDataTable();
            foreach (DataColumn col in emergencyAccessTable.Columns)
            {
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }

            emergencyAccessTable.PrimaryKey = new DataColumn[] { emergencyAccessTable.Columns[0] };
            bulkCopy.WriteToServer(emergencyAccessTable);
        }

        // Update emergency access table from temp table
        var sql = @"
                UPDATE
                    [dbo].[EmergencyAccess]
                SET
                    [KeyEncrypted] = TE.[KeyEncrypted],
                FROM
                    [dbo].[EmergencyAccess] E
                INNER JOIN
                    #TempEmergencyAccess TE ON E.Id = TE.Id
                WHERE
                    S.[UserId] = @UserId

                DROP TABLE #TempEmergencyAccess";

        using (var cmd = new SqlCommand(sql, connection, transaction))
        {
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.ExecuteNonQuery();
        }

    }
}

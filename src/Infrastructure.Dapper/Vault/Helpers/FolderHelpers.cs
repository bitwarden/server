using System.Data;
using Bit.Core.Vault.Entities;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Vault.Helpers;

public static class FolderHelpers
{
    public static DataTable ToDataTable(this IEnumerable<Folder> folders)
    {
        var foldersTable = new DataTable();
        foldersTable.SetTypeName("[dbo].[Folder]");

        var columnData = new List<(string name, Type type, Func<Folder, object> getter)>
        {
            (nameof(Folder.Id), typeof(Guid), c => c.Id),
            (nameof(Folder.UserId), typeof(Guid), c => c.UserId),
            (nameof(Folder.Name), typeof(string), c => c.Name),
            (nameof(Folder.CreationDate), typeof(DateTime), c => c.CreationDate),
            (nameof(Folder.RevisionDate), typeof(DateTime), c => c.RevisionDate),
        };

        return folders.BuildTable(foldersTable, columnData);
    }

    public static void UpdateEncryptedData(this IEnumerable<Folder> folders, Guid userId, SqlConnection connection, SqlTransaction transaction)
    {
        // Create temp table
        var sqlCreateTemp = @"
                            SELECT TOP 0 *
                            INTO #TempFolder
                            FROM [dbo].[Folder]";

        using (var cmd = new SqlCommand(sqlCreateTemp, connection, transaction))
        {
            cmd.ExecuteNonQuery();
        }

        // Bulk copy data into temp table
        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
        {
            bulkCopy.DestinationTableName = "#TempFolder";
            var foldersTable = folders.ToDataTable();
            foreach (DataColumn col in foldersTable.Columns)
            {
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }

            foldersTable.PrimaryKey = new DataColumn[] { foldersTable.Columns[0] };
            bulkCopy.WriteToServer(foldersTable);
        }

        // Update folder table from temp table
        var sql = @"
                    UPDATE
                        [dbo].[Folder]
                    SET
                        [Name] = TF.[Name],
                        [RevisionDate] = TF.[RevisionDate]
                    FROM
                        [dbo].[Folder] F
                    INNER JOIN
                        #TempFolder TF ON F.Id = TF.Id
                    WHERE
                        F.[UserId] = @UserId;

                    DROP TABLE #TempFolder";

        using (var cmd = new SqlCommand(sql, connection, transaction))
        {
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.ExecuteNonQuery();
        }
    }
}

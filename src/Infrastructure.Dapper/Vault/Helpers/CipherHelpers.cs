using System.Data;
using Bit.Core.Vault.Entities;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Vault.Helpers;

public static class CipherHelpers
{

    public static DataTable ToDataTable(this IEnumerable<Cipher> ciphers)
    {
        var ciphersTable = new DataTable();
        ciphersTable.SetTypeName("[dbo].[Cipher]");

        var columnData = new List<(string name, Type type, Func<Cipher, object> getter)>
        {
            (nameof(Cipher.Id), typeof(Guid), c => c.Id),
            (nameof(Cipher.UserId), typeof(Guid), c => c.UserId),
            (nameof(Cipher.OrganizationId), typeof(Guid), c => c.OrganizationId),
            (nameof(Cipher.Type), typeof(short), c => c.Type),
            (nameof(Cipher.Data), typeof(string), c => c.Data),
            (nameof(Cipher.Favorites), typeof(string), c => c.Favorites),
            (nameof(Cipher.Folders), typeof(string), c => c.Folders),
            (nameof(Cipher.Attachments), typeof(string), c => c.Attachments),
            (nameof(Cipher.CreationDate), typeof(DateTime), c => c.CreationDate),
            (nameof(Cipher.RevisionDate), typeof(DateTime), c => c.RevisionDate),
            (nameof(Cipher.DeletedDate), typeof(DateTime), c => c.DeletedDate),
            (nameof(Cipher.Reprompt), typeof(short), c => c.Reprompt),
            (nameof(Cipher.Key), typeof(string), c => c.Key),
        };

        return ciphers.BuildTable(ciphersTable, columnData);
    }

    public static void UpdateEncryptedData(this IEnumerable<Cipher> ciphers, Guid userId, SqlConnection connection, SqlTransaction transaction)
    {
        // Create temp table
        var sqlCreateTemp = @"
                            SELECT TOP 0 *
                            INTO #TempCipher
                            FROM [dbo].[Cipher]";

        using (var cmd = new SqlCommand(sqlCreateTemp, connection, transaction))
        {
            cmd.ExecuteNonQuery();
        }

        // Bulk copy data into temp table
        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
        {
            bulkCopy.DestinationTableName = "#TempCipher";
            var ciphersTable = ciphers.ToDataTable();
            foreach (DataColumn col in ciphersTable.Columns)
            {
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }

            ciphersTable.PrimaryKey = new DataColumn[] { ciphersTable.Columns[0] };
            bulkCopy.WriteToServer(ciphersTable);
        }

        // Update cipher table from temp table
        var sql = @"
                    UPDATE
                        [dbo].[Cipher]
                    SET
                        [Data] = TC.[Data],
                        [Attachments] = TC.[Attachments],
                        [RevisionDate] = TC.[RevisionDate],
                        [Key] = TC.[Key]
                    FROM
                        [dbo].[Cipher] C
                    INNER JOIN
                        #TempCipher TC ON C.Id = TC.Id
                    WHERE
                        C.[UserId] = @UserId

                    DROP TABLE #TempCipher";

        using (var cmd = new SqlCommand(sql, connection, transaction))
        {
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.ExecuteNonQuery();
        }
    }

}

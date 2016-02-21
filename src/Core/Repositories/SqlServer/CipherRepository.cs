using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Bit.Core.Repositories.SqlServer.Models;
using DataTableProxy;
using Bit.Core.Domains;
using System.Data;

namespace Bit.Core.Repositories.SqlServer
{
    public class CipherRepository : BaseRepository, ICipherRepository
    {
        public CipherRepository(string connectionString)
            : base(connectionString)
        { }

        public Task UpdateUserEmailPasswordAndCiphersAsync(User user, IEnumerable<dynamic> ciphers)
        {
            var cleanedCiphers = ciphers.Where(c => c is Cipher);
            if(cleanedCiphers.Count() == 0)
            {
                return Task.FromResult(0);
            }

            using(var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using(var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Update user.

                        using(var cmd = new SqlCommand("[dbo].[User_UpdateEmailPassword]", connection, transaction))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = new Guid(user.Id);
                            cmd.Parameters.Add("@Email", SqlDbType.NVarChar).Value = user.Email;
                            cmd.Parameters.Add("@MasterPassword", SqlDbType.NVarChar).Value = user.MasterPassword;
                            cmd.Parameters.Add("@SecurityStamp", SqlDbType.NVarChar).Value = user.SecurityStamp;
                            cmd.Parameters.Add("@RevisionDate", SqlDbType.DateTime2).Value = user.RevisionDate;
                            cmd.ExecuteNonQuery();
                        }

                        // 2. Create temp tables to bulk copy into.

                        var sqlCreateTemp = @"
                            SELECT TOP 0 *
                            INTO #TempFolder
                            FROM [dbo].[Folder]

                            SELECT TOP 0 *
                            INTO #TempSite
                            FROM [dbo].[Site]";

                        using(var cmd = new SqlCommand(sqlCreateTemp, connection, transaction))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        // 3. Bulk bopy into temp tables.

                        using(var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                        {
                            bulkCopy.DestinationTableName = "#TempFolder";

                            var dataTable = cleanedCiphers
                                .Where(c => c is Folder)
                                .Select(c => new FolderTableModel(c as Folder))
                                .ToTable(new ClassMapping<FolderTableModel>().AddAllPropertiesAsColumns());

                            bulkCopy.WriteToServer(dataTable);
                        }

                        using(var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                        {
                            bulkCopy.DestinationTableName = "#TempSite";

                            var dataTable = cleanedCiphers
                                .Where(c => c is Site)
                                .Select(c => new SiteTableModel(c as Site))
                                .ToTable(new ClassMapping<SiteTableModel>().AddAllPropertiesAsColumns());

                            bulkCopy.WriteToServer(dataTable);
                        }

                        // 4. Insert into real tables from temp tables and clean up.

                        var sqlUpdate = @"
                            UPDATE
                                [dbo].[Folder]
                            SET
                                -- Do not update [UserId]
                                [Name] = TF.[Name],
                                -- Do not update TF.[CreationDate]
                                [RevisionDate] = TF.[RevisionDate]
                            FROM
                                [dbo].[Folder] F
                            INNER JOIN
                                #TempFolder TF ON F.Id = TF.Id
                            WHERE
                                F.[UserId] = @UserId

                            UPDATE
                                [dbo].[Site]
                            SET
                                -- Do not update [UserId]
                                -- Do not update [FolderId]
                                [Name] = TS.[Name],
                                [Uri] = TS.[Uri],
                                [Username] = TS.[Username],
                                [Password] = TS.[Password],
                                [Notes] = TS.[Notes],
                                -- Do not update [CreationDate]
                                [RevisionDate] = TS.[RevisionDate]
                            FROM
                                [dbo].[Site] S
                            INNER JOIN
                                #TempSite TS ON S.Id = TS.Id
                            WHERE
                                S.[UserId] = @UserId

                            DROP TABLE #TempFolder
                            DROP TABLE #TempSite";

                        using(var cmd = new SqlCommand(sqlUpdate, connection, transaction))
                        {
                            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = new Guid(user.Id);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            return Task.FromResult(0);
        }

        public Task CreateAsync(IEnumerable<dynamic> ciphers)
        {
            var cleanedCiphers = ciphers.Where(c => c is Cipher);
            if(cleanedCiphers.Count() == 0)
            {
                return Task.FromResult(0);
            }

            // Generate new Ids for these new ciphers
            foreach(var cipher in cleanedCiphers)
            {
                cipher.Id = GenerateComb().ToString();
            }

            using(var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using(var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        using(var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                        {
                            bulkCopy.DestinationTableName = "[dbo].[Folder]";

                            var dataTable = cleanedCiphers
                                .Where(c => c is Folder)
                                .Select(c => new FolderTableModel(c as Folder))
                                .ToTable(new ClassMapping<FolderTableModel>().AddAllPropertiesAsColumns());

                            bulkCopy.WriteToServer(dataTable);
                        }

                        using(var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                        {
                            bulkCopy.DestinationTableName = "[dbo].[Site]";

                            var dataTable = cleanedCiphers
                                .Where(c => c is Site)
                                .Select(c => new SiteTableModel(c as Site))
                                .ToTable(new ClassMapping<SiteTableModel>().AddAllPropertiesAsColumns());

                            bulkCopy.WriteToServer(dataTable);
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            return Task.FromResult(0);
        }
    }
}

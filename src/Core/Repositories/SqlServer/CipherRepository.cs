using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using DataTableProxy;
using Bit.Core.Domains;
using System.Data;
using Dapper;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.SqlServer
{
    public class CipherRepository : Repository<Cipher, Guid>, ICipherRepository
    {
        public CipherRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public CipherRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<Cipher> GetByIdAsync(Guid id, Guid userId)
        {
            var cipher = await GetByIdAsync(id);
            if(cipher == null || cipher.UserId != userId)
            {
                return null;
            }

            return cipher;
        }

        public async Task<CipherShare> GetShareByIdAsync(Guid id, Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<CipherShare>(
                    $"[{Schema}].[CipherShare_ReadById]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.FirstOrDefault(c => c.UserId == userId);
            }
        }

        public async Task<ICollection<Cipher>> GetManyByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Cipher>(
                    $"[{Schema}].[{Table}_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<CipherShare>> GetManyShareByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<CipherShare>(
                    $"[{Schema}].[CipherShare_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<Cipher>> GetManyByTypeAndUserIdAsync(Enums.CipherType type, Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Cipher>(
                    $"[{Schema}].[{Table}_ReadByTypeUserId]",
                    new
                    {
                        Type = type,
                        UserId = userId
                    },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<Tuple<ICollection<Cipher>, ICollection<Guid>>>
            GetManySinceRevisionDateAndUserIdWithDeleteHistoryAsync(DateTime sinceRevisionDate, Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryMultipleAsync(
                    $"[{Schema}].[{Table}_ReadByRevisionDateUserWithDeleteHistory]",
                    new
                    {
                        SinceRevisionDate = sinceRevisionDate,
                        UserId = userId
                    },
                    commandType: CommandType.StoredProcedure);

                var ciphers = await results.ReadAsync<Cipher>();
                var deletes = await results.ReadAsync<Guid>();

                return new Tuple<ICollection<Cipher>, ICollection<Guid>>(ciphers.ToList(), deletes.ToList());
            }
        }

        public Task UpdateUserEmailPasswordAndCiphersAsync(User user, IEnumerable<Cipher> ciphers)
        {
            if(ciphers.Count() == 0)
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
                            cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = user.Id;
                            cmd.Parameters.Add("@Email", SqlDbType.NVarChar).Value = user.Email;
                            cmd.Parameters.Add("@EmailVerified", SqlDbType.NVarChar).Value = user.EmailVerified;
                            cmd.Parameters.Add("@MasterPassword", SqlDbType.NVarChar).Value = user.MasterPassword;
                            cmd.Parameters.Add("@SecurityStamp", SqlDbType.NVarChar).Value = user.SecurityStamp;
                            cmd.Parameters.Add("@RevisionDate", SqlDbType.DateTime2).Value = user.RevisionDate;
                            cmd.ExecuteNonQuery();
                        }

                        // 2. Create temp tables to bulk copy into.

                        var sqlCreateTemp = @"
                            SELECT TOP 0 *
                            INTO #TempCipher
                            FROM [dbo].[Cipher]";

                        using(var cmd = new SqlCommand(sqlCreateTemp, connection, transaction))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        // 3. Bulk bopy into temp tables.

                        using(var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                        {
                            bulkCopy.DestinationTableName = "#TempCipher";

                            var dataTable = ciphers.ToTable(new ClassMapping<Cipher>().AddAllPropertiesAsColumns());
                            bulkCopy.WriteToServer(dataTable);
                        }

                        // 4. Insert into real tables from temp tables and clean up.

                        var sqlUpdate = @"
                            UPDATE
                                [dbo].[Cipher]
                            SET
                                -- Do not update [UserId]
                                -- Do not update [FolderId]
                                -- Do not update [Type]
                                -- Do not update [Favorite]
                                [Data] = TC.[Data],
                                -- Do not update [CreationDate]
                                [RevisionDate] = TC.[RevisionDate]
                            FROM
                                [dbo].[Cipher] C
                            INNER JOIN
                                #TempCipher TC ON C.Id = TC.Id
                            WHERE
                                C.[UserId] = @UserId

                            DROP TABLE #TempCipher";

                        using(var cmd = new SqlCommand(sqlUpdate, connection, transaction))
                        {
                            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = user.Id;
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

        public Task CreateAsync(IEnumerable<Cipher> ciphers)
        {
            if(ciphers.Count() == 0)
            {
                return Task.FromResult(0);
            }

            // Generate new Ids for these new ciphers
            foreach(var cipher in ciphers)
            {
                cipher.SetNewId();
            }

            using(var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using(var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        using(var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.FireTriggers, transaction))
                        {
                            bulkCopy.DestinationTableName = "[dbo].[Cipher]";
                            var dataTable = ciphers.ToTable(new ClassMapping<Cipher>().AddAllPropertiesAsColumns());
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

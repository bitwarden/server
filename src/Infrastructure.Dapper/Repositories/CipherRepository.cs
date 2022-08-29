using System.Data;
using System.Data.SqlClient;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Core.Models.Data;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class CipherRepository : Repository<Cipher, Guid>, ICipherRepository
{
    public CipherRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public CipherRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<CipherDetails> GetByIdAsync(Guid id, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<CipherDetails>(
                $"[{Schema}].[CipherDetails_ReadByIdUserId]",
                new { Id = id, UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.FirstOrDefault();
        }
    }

    public async Task<CipherOrganizationDetails> GetOrganizationDetailsByIdAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<CipherDetails>(
                $"[{Schema}].[CipherOrganizationDetails_ReadById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);

            return results.FirstOrDefault();
        }
    }

    public async Task<ICollection<CipherOrganizationDetails>> GetManyOrganizationDetailsByOrganizationIdAsync(
        Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<CipherOrganizationDetails>(
                $"[{Schema}].[CipherOrganizationDetails_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<bool> GetCanEditByIdAsync(Guid userId, Guid cipherId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var result = await connection.QueryFirstOrDefaultAsync<bool>(
                $"[{Schema}].[Cipher_ReadCanEditByIdUserId]",
                new { UserId = userId, Id = cipherId },
                commandType: CommandType.StoredProcedure);

            return result;
        }
    }

    public async Task<ICollection<CipherDetails>> GetManyByUserIdAsync(Guid userId, bool withOrganizations = true)
    {
        string sprocName = null;
        if (withOrganizations)
        {
            sprocName = $"[{Schema}].[CipherDetails_ReadByUserId]";
        }
        else
        {
            sprocName = $"[{Schema}].[CipherDetails_ReadWithoutOrganizationsByUserId]";
        }

        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<CipherDetails>(
                sprocName,
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results
                .GroupBy(c => c.Id)
                .Select(g => g.OrderByDescending(og => og.Edit).First())
                .ToList();
        }
    }

    public async Task<ICollection<Cipher>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Cipher>(
                $"[{Schema}].[Cipher_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task CreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
    {
        cipher.SetNewId();
        var objWithCollections = JsonSerializer.Deserialize<CipherWithCollections>(
            JsonSerializer.Serialize(cipher));
        objWithCollections.CollectionIds = collectionIds.ToGuidIdArrayTVP();
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Cipher_CreateWithCollections]",
                objWithCollections,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task CreateAsync(CipherDetails cipher)
    {
        cipher.SetNewId();
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[CipherDetails_Create]",
                cipher,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task CreateAsync(CipherDetails cipher, IEnumerable<Guid> collectionIds)
    {
        cipher.SetNewId();
        var objWithCollections = JsonSerializer.Deserialize<CipherDetailsWithCollections>(
            JsonSerializer.Serialize(cipher));
        objWithCollections.CollectionIds = collectionIds.ToGuidIdArrayTVP();
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[CipherDetails_CreateWithCollections]",
                objWithCollections,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task ReplaceAsync(CipherDetails obj)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[CipherDetails_Update]",
                obj,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task UpsertAsync(CipherDetails cipher)
    {
        if (cipher.Id.Equals(default))
        {
            await CreateAsync(cipher);
        }
        else
        {
            await ReplaceAsync(cipher);
        }
    }

    public async Task<bool> ReplaceAsync(Cipher obj, IEnumerable<Guid> collectionIds)
    {
        var objWithCollections = JsonSerializer.Deserialize<CipherWithCollections>(
            JsonSerializer.Serialize(obj));
        objWithCollections.CollectionIds = collectionIds.ToGuidIdArrayTVP();

        using (var connection = new SqlConnection(ConnectionString))
        {
            var result = await connection.ExecuteScalarAsync<int>(
                $"[{Schema}].[Cipher_UpdateWithCollections]",
                objWithCollections,
                commandType: CommandType.StoredProcedure);
            return result >= 0;
        }
    }

    public async Task UpdatePartialAsync(Guid id, Guid userId, Guid? folderId, bool favorite)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Cipher_UpdatePartial]",
                new { Id = id, UserId = userId, FolderId = folderId, Favorite = favorite },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task UpdateAttachmentAsync(CipherAttachment attachment)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Cipher_UpdateAttachment]",
                attachment,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task DeleteAttachmentAsync(Guid cipherId, string attachmentId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Cipher_DeleteAttachment]",
                new { Id = cipherId, AttachmentId = attachmentId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task DeleteAsync(IEnumerable<Guid> ids, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Cipher_Delete]",
                new { Ids = ids.ToGuidIdArrayTVP(), UserId = userId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task DeleteByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Cipher_DeleteByIdsOrganizationId]",
                new { Ids = ids.ToGuidIdArrayTVP(), OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task SoftDeleteByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Cipher_SoftDeleteByIdsOrganizationId]",
                new { Ids = ids.ToGuidIdArrayTVP(), OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task MoveAsync(IEnumerable<Guid> ids, Guid? folderId, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Cipher_Move]",
                new { Ids = ids.ToGuidIdArrayTVP(), FolderId = folderId, UserId = userId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task DeleteByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Cipher_DeleteByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task DeleteByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Cipher_DeleteByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public Task UpdateUserKeysAndCiphersAsync(User user, IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders, IEnumerable<Send> sends)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // 1. Update user.

                    using (var cmd = new SqlCommand("[dbo].[User_UpdateKeys]", connection, transaction))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = user.Id;
                        cmd.Parameters.Add("@SecurityStamp", SqlDbType.NVarChar).Value = user.SecurityStamp;
                        cmd.Parameters.Add("@Key", SqlDbType.VarChar).Value = user.Key;

                        if (string.IsNullOrWhiteSpace(user.PrivateKey))
                        {
                            cmd.Parameters.Add("@PrivateKey", SqlDbType.VarChar).Value = DBNull.Value;
                        }
                        else
                        {
                            cmd.Parameters.Add("@PrivateKey", SqlDbType.VarChar).Value = user.PrivateKey;
                        }

                        cmd.Parameters.Add("@RevisionDate", SqlDbType.DateTime2).Value = user.RevisionDate;
                        cmd.ExecuteNonQuery();
                    }

                    // 2. Create temp tables to bulk copy into.

                    var sqlCreateTemp = @"
                            SELECT TOP 0 *
                            INTO #TempCipher
                            FROM [dbo].[Cipher]

                            SELECT TOP 0 *
                            INTO #TempFolder
                            FROM [dbo].[Folder]

                            SELECT TOP 0 *
                            INTO #TempSend
                            FROM [dbo].[Send]";

                    using (var cmd = new SqlCommand(sqlCreateTemp, connection, transaction))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // 3. Bulk copy into temp tables.

                    if (ciphers.Any())
                    {
                        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                        {
                            bulkCopy.DestinationTableName = "#TempCipher";
                            var dataTable = BuildCiphersTable(bulkCopy, ciphers);
                            bulkCopy.WriteToServer(dataTable);
                        }
                    }

                    if (folders.Any())
                    {
                        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                        {
                            bulkCopy.DestinationTableName = "#TempFolder";
                            var dataTable = BuildFoldersTable(bulkCopy, folders);
                            bulkCopy.WriteToServer(dataTable);
                        }
                    }

                    if (sends.Any())
                    {
                        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                        {
                            bulkCopy.DestinationTableName = "#TempSend";
                            var dataTable = BuildSendsTable(bulkCopy, sends);
                            bulkCopy.WriteToServer(dataTable);
                        }
                    }

                    // 4. Insert into real tables from temp tables and clean up.

                    var sql = string.Empty;

                    if (ciphers.Any())
                    {
                        sql += @"
                                UPDATE
                                    [dbo].[Cipher]
                                SET
                                    [Data] = TC.[Data],
                                    [Attachments] = TC.[Attachments],
                                    [RevisionDate] = TC.[RevisionDate]
                                FROM
                                    [dbo].[Cipher] C
                                INNER JOIN
                                    #TempCipher TC ON C.Id = TC.Id
                                WHERE
                                    C.[UserId] = @UserId";
                    }

                    if (folders.Any())
                    {
                        sql += @"
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
                                    F.[UserId] = @UserId";
                    }

                    if (sends.Any())
                    {
                        sql += @"
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
                                    S.[UserId] = @UserId";
                    }

                    sql += @"
                            DROP TABLE #TempCipher
                            DROP TABLE #TempFolder
                            DROP TABLE #TempSend";

                    using (var cmd = new SqlCommand(sql, connection, transaction))
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

    public async Task UpdateCiphersAsync(Guid userId, IEnumerable<Cipher> ciphers)
    {
        if (!ciphers.Any())
        {
            return;
        }

        using (var connection = new SqlConnection(ConnectionString))
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // 1. Create temp tables to bulk copy into.

                    var sqlCreateTemp = @"
                            SELECT TOP 0 *
                            INTO #TempCipher
                            FROM [dbo].[Cipher]";

                    using (var cmd = new SqlCommand(sqlCreateTemp, connection, transaction))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // 2. Bulk copy into temp tables.
                    using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                    {
                        bulkCopy.DestinationTableName = "#TempCipher";
                        var dataTable = BuildCiphersTable(bulkCopy, ciphers);
                        bulkCopy.WriteToServer(dataTable);
                    }

                    // 3. Insert into real tables from temp tables and clean up.

                    // Intentionally not including Favorites, Folders, and CreationDate
                    // since those are not meant to be bulk updated at this time
                    var sql = @"
                            UPDATE
                                [dbo].[Cipher]
                            SET
                                [UserId] = TC.[UserId],
                                [OrganizationId] = TC.[OrganizationId],
                                [Type] = TC.[Type],
                                [Data] = TC.[Data],
                                [Attachments] = TC.[Attachments],
                                [RevisionDate] = TC.[RevisionDate],
                                [DeletedDate] = TC.[DeletedDate]
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

                    await connection.ExecuteAsync(
                        $"[{Schema}].[User_BumpAccountRevisionDate]",
                        new { Id = userId },
                        commandType: CommandType.StoredProcedure, transaction: transaction);

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    public async Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders)
    {
        if (!ciphers.Any())
        {
            return;
        }

        using (var connection = new SqlConnection(ConnectionString))
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    if (folders.Any())
                    {
                        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                        {
                            bulkCopy.DestinationTableName = "[dbo].[Folder]";
                            var dataTable = BuildFoldersTable(bulkCopy, folders);
                            bulkCopy.WriteToServer(dataTable);
                        }
                    }

                    using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                    {
                        bulkCopy.DestinationTableName = "[dbo].[Cipher]";
                        var dataTable = BuildCiphersTable(bulkCopy, ciphers);
                        bulkCopy.WriteToServer(dataTable);
                    }

                    await connection.ExecuteAsync(
                            $"[{Schema}].[User_BumpAccountRevisionDate]",
                            new { Id = ciphers.First().UserId },
                            commandType: CommandType.StoredProcedure, transaction: transaction);

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    public async Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Collection> collections,
        IEnumerable<CollectionCipher> collectionCiphers)
    {
        if (!ciphers.Any())
        {
            return;
        }

        using (var connection = new SqlConnection(ConnectionString))
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                    {
                        bulkCopy.DestinationTableName = "[dbo].[Cipher]";
                        var dataTable = BuildCiphersTable(bulkCopy, ciphers);
                        bulkCopy.WriteToServer(dataTable);
                    }

                    if (collections.Any())
                    {
                        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                        {
                            bulkCopy.DestinationTableName = "[dbo].[Collection]";
                            var dataTable = BuildCollectionsTable(bulkCopy, collections);
                            bulkCopy.WriteToServer(dataTable);
                        }

                        if (collectionCiphers.Any())
                        {
                            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                            {
                                bulkCopy.DestinationTableName = "[dbo].[CollectionCipher]";
                                var dataTable = BuildCollectionCiphersTable(bulkCopy, collectionCiphers);
                                bulkCopy.WriteToServer(dataTable);
                            }
                        }
                    }

                    await connection.ExecuteAsync(
                            $"[{Schema}].[User_BumpAccountRevisionDateByOrganizationId]",
                            new { OrganizationId = ciphers.First().OrganizationId },
                            commandType: CommandType.StoredProcedure, transaction: transaction);

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    public async Task SoftDeleteAsync(IEnumerable<Guid> ids, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[Cipher_SoftDelete]",
                new { Ids = ids.ToGuidIdArrayTVP(), UserId = userId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<DateTime> RestoreAsync(IEnumerable<Guid> ids, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteScalarAsync<DateTime>(
                $"[{Schema}].[Cipher_Restore]",
                new { Ids = ids.ToGuidIdArrayTVP(), UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results;
        }
    }

    public async Task DeleteDeletedAsync(DateTime deletedDateBefore)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                $"[{Schema}].[Cipher_DeleteDeleted]",
                new { DeletedDateBefore = deletedDateBefore },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 43200);
        }
    }

    private DataTable BuildCiphersTable(SqlBulkCopy bulkCopy, IEnumerable<Cipher> ciphers)
    {
        var c = ciphers.FirstOrDefault();
        if (c == null)
        {
            throw new ApplicationException("Must have some ciphers to bulk import.");
        }

        var ciphersTable = new DataTable("CipherDataTable");

        var idColumn = new DataColumn(nameof(c.Id), c.Id.GetType());
        ciphersTable.Columns.Add(idColumn);
        var userIdColumn = new DataColumn(nameof(c.UserId), typeof(Guid));
        ciphersTable.Columns.Add(userIdColumn);
        var organizationId = new DataColumn(nameof(c.OrganizationId), typeof(Guid));
        ciphersTable.Columns.Add(organizationId);
        var typeColumn = new DataColumn(nameof(c.Type), typeof(short));
        ciphersTable.Columns.Add(typeColumn);
        var dataColumn = new DataColumn(nameof(c.Data), typeof(string));
        ciphersTable.Columns.Add(dataColumn);
        var favoritesColumn = new DataColumn(nameof(c.Favorites), typeof(string));
        ciphersTable.Columns.Add(favoritesColumn);
        var foldersColumn = new DataColumn(nameof(c.Folders), typeof(string));
        ciphersTable.Columns.Add(foldersColumn);
        var attachmentsColumn = new DataColumn(nameof(c.Attachments), typeof(string));
        ciphersTable.Columns.Add(attachmentsColumn);
        var creationDateColumn = new DataColumn(nameof(c.CreationDate), c.CreationDate.GetType());
        ciphersTable.Columns.Add(creationDateColumn);
        var revisionDateColumn = new DataColumn(nameof(c.RevisionDate), c.RevisionDate.GetType());
        ciphersTable.Columns.Add(revisionDateColumn);
        var deletedDateColumn = new DataColumn(nameof(c.DeletedDate), typeof(DateTime));
        ciphersTable.Columns.Add(deletedDateColumn);
        var repromptColumn = new DataColumn(nameof(c.Reprompt), typeof(short));
        ciphersTable.Columns.Add(repromptColumn);

        foreach (DataColumn col in ciphersTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[1];
        keys[0] = idColumn;
        ciphersTable.PrimaryKey = keys;

        foreach (var cipher in ciphers)
        {
            var row = ciphersTable.NewRow();

            row[idColumn] = cipher.Id;
            row[userIdColumn] = cipher.UserId.HasValue ? (object)cipher.UserId.Value : DBNull.Value;
            row[organizationId] = cipher.OrganizationId.HasValue ? (object)cipher.OrganizationId.Value : DBNull.Value;
            row[typeColumn] = (short)cipher.Type;
            row[dataColumn] = cipher.Data;
            row[favoritesColumn] = cipher.Favorites;
            row[foldersColumn] = cipher.Folders;
            row[attachmentsColumn] = cipher.Attachments;
            row[creationDateColumn] = cipher.CreationDate;
            row[revisionDateColumn] = cipher.RevisionDate;
            row[deletedDateColumn] = cipher.DeletedDate.HasValue ? (object)cipher.DeletedDate : DBNull.Value;
            row[repromptColumn] = cipher.Reprompt;

            ciphersTable.Rows.Add(row);
        }

        return ciphersTable;
    }

    private DataTable BuildFoldersTable(SqlBulkCopy bulkCopy, IEnumerable<Folder> folders)
    {
        var f = folders.FirstOrDefault();
        if (f == null)
        {
            throw new ApplicationException("Must have some folders to bulk import.");
        }

        var foldersTable = new DataTable("FolderDataTable");

        var idColumn = new DataColumn(nameof(f.Id), f.Id.GetType());
        foldersTable.Columns.Add(idColumn);
        var userIdColumn = new DataColumn(nameof(f.UserId), f.UserId.GetType());
        foldersTable.Columns.Add(userIdColumn);
        var nameColumn = new DataColumn(nameof(f.Name), typeof(string));
        foldersTable.Columns.Add(nameColumn);
        var creationDateColumn = new DataColumn(nameof(f.CreationDate), f.CreationDate.GetType());
        foldersTable.Columns.Add(creationDateColumn);
        var revisionDateColumn = new DataColumn(nameof(f.RevisionDate), f.RevisionDate.GetType());
        foldersTable.Columns.Add(revisionDateColumn);

        foreach (DataColumn col in foldersTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[1];
        keys[0] = idColumn;
        foldersTable.PrimaryKey = keys;

        foreach (var folder in folders)
        {
            var row = foldersTable.NewRow();

            row[idColumn] = folder.Id;
            row[userIdColumn] = folder.UserId;
            row[nameColumn] = folder.Name;
            row[creationDateColumn] = folder.CreationDate;
            row[revisionDateColumn] = folder.RevisionDate;

            foldersTable.Rows.Add(row);
        }

        return foldersTable;
    }

    private DataTable BuildCollectionsTable(SqlBulkCopy bulkCopy, IEnumerable<Collection> collections)
    {
        var c = collections.FirstOrDefault();
        if (c == null)
        {
            throw new ApplicationException("Must have some collections to bulk import.");
        }

        var collectionsTable = new DataTable("CollectionDataTable");

        var idColumn = new DataColumn(nameof(c.Id), c.Id.GetType());
        collectionsTable.Columns.Add(idColumn);
        var organizationIdColumn = new DataColumn(nameof(c.OrganizationId), c.OrganizationId.GetType());
        collectionsTable.Columns.Add(organizationIdColumn);
        var nameColumn = new DataColumn(nameof(c.Name), typeof(string));
        collectionsTable.Columns.Add(nameColumn);
        var creationDateColumn = new DataColumn(nameof(c.CreationDate), c.CreationDate.GetType());
        collectionsTable.Columns.Add(creationDateColumn);
        var revisionDateColumn = new DataColumn(nameof(c.RevisionDate), c.RevisionDate.GetType());
        collectionsTable.Columns.Add(revisionDateColumn);

        foreach (DataColumn col in collectionsTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[1];
        keys[0] = idColumn;
        collectionsTable.PrimaryKey = keys;

        foreach (var collection in collections)
        {
            var row = collectionsTable.NewRow();

            row[idColumn] = collection.Id;
            row[organizationIdColumn] = collection.OrganizationId;
            row[nameColumn] = collection.Name;
            row[creationDateColumn] = collection.CreationDate;
            row[revisionDateColumn] = collection.RevisionDate;

            collectionsTable.Rows.Add(row);
        }

        return collectionsTable;
    }

    private DataTable BuildCollectionCiphersTable(SqlBulkCopy bulkCopy, IEnumerable<CollectionCipher> collectionCiphers)
    {
        var cc = collectionCiphers.FirstOrDefault();
        if (cc == null)
        {
            throw new ApplicationException("Must have some collectionCiphers to bulk import.");
        }

        var collectionCiphersTable = new DataTable("CollectionCipherDataTable");

        var collectionIdColumn = new DataColumn(nameof(cc.CollectionId), cc.CollectionId.GetType());
        collectionCiphersTable.Columns.Add(collectionIdColumn);
        var cipherIdColumn = new DataColumn(nameof(cc.CipherId), cc.CipherId.GetType());
        collectionCiphersTable.Columns.Add(cipherIdColumn);

        foreach (DataColumn col in collectionCiphersTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[2];
        keys[0] = collectionIdColumn;
        keys[1] = cipherIdColumn;
        collectionCiphersTable.PrimaryKey = keys;

        foreach (var collectionCipher in collectionCiphers)
        {
            var row = collectionCiphersTable.NewRow();

            row[collectionIdColumn] = collectionCipher.CollectionId;
            row[cipherIdColumn] = collectionCipher.CipherId;

            collectionCiphersTable.Rows.Add(row);
        }

        return collectionCiphersTable;
    }

    private DataTable BuildSendsTable(SqlBulkCopy bulkCopy, IEnumerable<Send> sends)
    {
        var s = sends.FirstOrDefault();
        if (s == null)
        {
            throw new ApplicationException("Must have some Sends to bulk import.");
        }

        var sendsTable = new DataTable("SendsDataTable");

        var idColumn = new DataColumn(nameof(s.Id), s.Id.GetType());
        sendsTable.Columns.Add(idColumn);
        var userIdColumn = new DataColumn(nameof(s.UserId), typeof(Guid));
        sendsTable.Columns.Add(userIdColumn);
        var organizationIdColumn = new DataColumn(nameof(s.OrganizationId), typeof(Guid));
        sendsTable.Columns.Add(organizationIdColumn);
        var typeColumn = new DataColumn(nameof(s.Type), s.Type.GetType());
        sendsTable.Columns.Add(typeColumn);
        var dataColumn = new DataColumn(nameof(s.Data), s.Data.GetType());
        sendsTable.Columns.Add(dataColumn);
        var keyColumn = new DataColumn(nameof(s.Key), s.Key.GetType());
        sendsTable.Columns.Add(keyColumn);
        var passwordColumn = new DataColumn(nameof(s.Password), typeof(string));
        sendsTable.Columns.Add(passwordColumn);
        var maxAccessCountColumn = new DataColumn(nameof(s.MaxAccessCount), typeof(int));
        sendsTable.Columns.Add(maxAccessCountColumn);
        var accessCountColumn = new DataColumn(nameof(s.AccessCount), s.AccessCount.GetType());
        sendsTable.Columns.Add(accessCountColumn);
        var creationDateColumn = new DataColumn(nameof(s.CreationDate), s.CreationDate.GetType());
        sendsTable.Columns.Add(creationDateColumn);
        var revisionDateColumn = new DataColumn(nameof(s.RevisionDate), s.RevisionDate.GetType());
        sendsTable.Columns.Add(revisionDateColumn);
        var expirationDateColumn = new DataColumn(nameof(s.ExpirationDate), typeof(DateTime));
        sendsTable.Columns.Add(expirationDateColumn);
        var deletionDateColumn = new DataColumn(nameof(s.DeletionDate), s.DeletionDate.GetType());
        sendsTable.Columns.Add(deletionDateColumn);
        var disabledColumn = new DataColumn(nameof(s.Disabled), s.Disabled.GetType());
        sendsTable.Columns.Add(disabledColumn);
        var hideEmailColumn = new DataColumn(nameof(s.HideEmail), typeof(bool));
        sendsTable.Columns.Add(hideEmailColumn);

        foreach (DataColumn col in sendsTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[1];
        keys[0] = idColumn;
        sendsTable.PrimaryKey = keys;

        foreach (var send in sends)
        {
            var row = sendsTable.NewRow();

            row[idColumn] = send.Id;
            row[userIdColumn] = send.UserId.HasValue ? (object)send.UserId.Value : DBNull.Value;
            row[organizationIdColumn] = send.OrganizationId.HasValue ? (object)send.OrganizationId.Value : DBNull.Value;
            row[typeColumn] = (short)send.Type;
            row[dataColumn] = send.Data;
            row[keyColumn] = send.Key;
            row[passwordColumn] = send.Password;
            row[maxAccessCountColumn] = send.MaxAccessCount.HasValue ? (object)send.MaxAccessCount : DBNull.Value;
            row[accessCountColumn] = send.AccessCount;
            row[creationDateColumn] = send.CreationDate;
            row[revisionDateColumn] = send.RevisionDate;
            row[expirationDateColumn] = send.ExpirationDate.HasValue ? (object)send.ExpirationDate : DBNull.Value;
            row[deletionDateColumn] = send.DeletionDate;
            row[disabledColumn] = send.Disabled;
            row[hideEmailColumn] = send.HideEmail.HasValue ? (object)send.HideEmail : DBNull.Value;

            sendsTable.Rows.Add(row);
        }

        return sendsTable;
    }

    public class CipherDetailsWithCollections : CipherDetails
    {
        public DataTable CollectionIds { get; set; }
    }

    public class CipherWithCollections : Cipher
    {
        public DataTable CollectionIds { get; set; }
    }
}

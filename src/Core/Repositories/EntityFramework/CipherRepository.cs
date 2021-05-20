using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Core.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework.Queries;

namespace Bit.Core.Repositories.EntityFramework
{
    public class CipherRepository : Repository<TableModel.Cipher, EfModel.Cipher, Guid>, ICipherRepository
    {
        public CipherRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Ciphers)
        { }

        public override async Task<Cipher> CreateAsync(Cipher cipher)
        {
            cipher = await base.CreateAsync(cipher);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                if (cipher.OrganizationId.HasValue)
                {
                    var query = new UserBumpAccountRevisionDateByCipherId(cipher);
                    var users = query.Run(dbContext);

                    await users.ForEachAsync(e => {
                        dbContext.Entry(e).Property(p => p.AccountRevisionDate).CurrentValue = DateTime.UtcNow;
                    });
                    await dbContext.SaveChangesAsync();
                }
                else
                {
                    var user = await dbContext.Users.FindAsync(cipher.UserId);
                    user.AccountRevisionDate = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync();
                }
            }
            return cipher;
        }

        public IQueryable<User> GetBumpedAccountsByCipherId(Cipher cipher)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = new UserBumpAccountRevisionDateByCipherId(cipher);
                return query.Run(dbContext);
            }
        }

        public async Task CreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        {
            cipher = await base.CreateAsync(cipher);
            await UpdateCollections(cipher, collectionIds);
        }

        private async Task UpdateCollections(Cipher cipher, IEnumerable<Guid> collectionIds)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = new CipherUpdateCollections(cipher, collectionIds);
                await dbContext.AddRangeAsync(query);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task CreateAsync(CipherDetails cipher)
        {
            await CreateAsyncReturnCipher(cipher);
        }

        private async Task<CipherDetails> CreateAsyncReturnCipher(CipherDetails cipher)
        {
            cipher.SetNewId();
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                cipher.SetNewId();
                var userIdKey = $"\"{cipher.UserId}\"";
                cipher.UserId = cipher.OrganizationId.HasValue ? 
                    null : 
                    cipher.UserId;
                cipher.Favorites = cipher.Favorite ? 
                    $"{{{userIdKey}:true}}" : 
                    null;
                cipher.Folders = cipher.FolderId.HasValue ?
                    $"{{{userIdKey}:\"{cipher.FolderId}\"}}" :
                    null;
                var entity = Mapper.Map<EfModel.Cipher>((TableModel.Cipher)cipher);
                await dbContext.AddAsync(entity);
                await dbContext.SaveChangesAsync();
            }
            return cipher;
        }

        public async Task CreateAsync(CipherDetails cipher, IEnumerable<Guid> collectionIds)
        {
            cipher = await CreateAsyncReturnCipher(cipher);
            await UpdateCollections(cipher, collectionIds);
        }

        public Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders)
        {
            throw new NotImplementedException();
            /* if (!ciphers.Any()) */
            /* { */
            /*     return; */
            /* } */

            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     connection.Open(); */

            /*     using (var transaction = connection.BeginTransaction()) */
            /*     { */
            /*         try */
            /*         { */
            /*             if (folders.Any()) */
            /*             { */
            /*                 using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction)) */
            /*                 { */
            /*                     bulkCopy.DestinationTableName = "[dbo].[Folder]"; */
            /*                     var dataTable = BuildFoldersTable(bulkCopy, folders); */
            /*                     bulkCopy.WriteToServer(dataTable); */
            /*                 } */
            /*             } */

            /*             using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction)) */
            /*             { */
            /*                 bulkCopy.DestinationTableName = "[dbo].[Cipher]"; */
            /*                 var dataTable = BuildCiphersTable(bulkCopy, ciphers); */
            /*                 bulkCopy.WriteToServer(dataTable); */
            /*             } */

            /*             await connection.ExecuteAsync( */
            /*                     $"[{Schema}].[User_BumpAccountRevisionDate]", */
            /*                     new { Id = ciphers.First().UserId }, */
            /*                     commandType: CommandType.StoredProcedure, transaction: transaction); */

            /*             transaction.Commit(); */
            /*         } */
            /*         catch */
            /*         { */
            /*             transaction.Rollback(); */
            /*             throw; */
            /*         } */
            /*     } */
            /* } */
        }

        public Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Collection> collections, IEnumerable<CollectionCipher> collectionCiphers)
        {
            throw new NotImplementedException();
            /* if (!ciphers.Any()) */
            /* { */
            /*     return; */
            /* } */

            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     connection.Open(); */

            /*     using (var transaction = connection.BeginTransaction()) */
            /*     { */
            /*         try */
            /*         { */
            /*             using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction)) */
            /*             { */
            /*                 bulkCopy.DestinationTableName = "[dbo].[Cipher]"; */
            /*                 var dataTable = BuildCiphersTable(bulkCopy, ciphers); */
            /*                 bulkCopy.WriteToServer(dataTable); */
            /*             } */

            /*             if (collections.Any()) */
            /*             { */
            /*                 using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction)) */
            /*                 { */
            /*                     bulkCopy.DestinationTableName = "[dbo].[Collection]"; */
            /*                     var dataTable = BuildCollectionsTable(bulkCopy, collections); */
            /*                     bulkCopy.WriteToServer(dataTable); */
            /*                 } */

            /*                 if (collectionCiphers.Any()) */
            /*                 { */
            /*                     using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction)) */
            /*                     { */
            /*                         bulkCopy.DestinationTableName = "[dbo].[CollectionCipher]"; */
            /*                         var dataTable = BuildCollectionCiphersTable(bulkCopy, collectionCiphers); */
            /*                         bulkCopy.WriteToServer(dataTable); */
            /*                     } */
            /*                 } */
            /*             } */

            /*             await connection.ExecuteAsync( */
            /*                     $"[{Schema}].[User_BumpAccountRevisionDateByOrganizationId]", */
            /*                     new { OrganizationId = ciphers.First().OrganizationId }, */
            /*                     commandType: CommandType.StoredProcedure, transaction: transaction); */

            /*             transaction.Commit(); */
            /*         } */
            /*         catch */
            /*         { */
            /*             transaction.Rollback(); */
            /*             throw; */
            /*         } */
            /*     } */
            /* } */
        }

        public Task DeleteAsync(IEnumerable<Guid> ids, Guid userId)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var results = await connection.ExecuteAsync( */
            /*         $"[{Schema}].[Cipher_Delete]", */
            /*         new { Ids = ids.ToGuidIdArrayTVP(), UserId = userId }, */
            /*         commandType: CommandType.StoredProcedure); */
            /* } */
        }

        public Task DeleteAttachmentAsync(Guid cipherId, string attachmentId)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var results = await connection.ExecuteAsync( */
            /*         $"[{Schema}].[Cipher_DeleteAttachment]", */
            /*         new { Id = cipherId, AttachmentId = attachmentId }, */
            /*         commandType: CommandType.StoredProcedure); */
            /* } */
        }

        public Task DeleteByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var results = await connection.ExecuteAsync( */
            /*         $"[{Schema}].[Cipher_DeleteByIdsOrganizationId]", */
            /*         new { Ids = ids.ToGuidIdArrayTVP(), OrganizationId = organizationId }, */
            /*         commandType: CommandType.StoredProcedure); */
            /* } */
        }

        public Task DeleteByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
            /* public async Task DeleteByOrganizationIdAsync(Guid organizationId) */
            /* { */
            /*     using (var connection = new SqlConnection(ConnectionString)) */
            /*     { */
            /*         var results = await connection.ExecuteAsync( */
            /*             $"[{Schema}].[Cipher_DeleteByOrganizationId]", */
            /*             new { OrganizationId = organizationId }, */
            /*             commandType: CommandType.StoredProcedure); */
            /*     } */
            /* } */
        }

        public Task DeleteByUserIdAsync(Guid userId)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var results = await connection.ExecuteAsync( */
            /*         $"[{Schema}].[Cipher_DeleteByUserId]", */
            /*         new { UserId = userId }, */
            /*         commandType: CommandType.StoredProcedure); */
            /* } */
        }

        public Task DeleteDeletedAsync(DateTime deletedDateBefore)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     await connection.ExecuteAsync( */
            /*         $"[{Schema}].[Cipher_DeleteDeleted]", */
            /*         new { DeletedDateBefore = deletedDateBefore }, */
            /*         commandType: CommandType.StoredProcedure, */
            /*         commandTimeout: 43200); */
            /* } */
        }

        // TODO: id param isnt used here?
        public async Task<CipherDetails> GetByIdAsync(Guid id, Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var userCipherDetails = new UserCipherDetailsQuery(userId);
                return await userCipherDetails.Run(dbContext).FirstOrDefaultAsync();
            }
        }

        public Task<bool> GetCanEditByIdAsync(Guid userId, Guid cipherId)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var result = await connection.QueryFirstOrDefaultAsync<bool>( */
            /*         $"[{Schema}].[Cipher_ReadCanEditByIdUserId]", */
            /*         new { UserId = userId, Id = cipherId }, */
            /*         commandType: CommandType.StoredProcedure); */

            /*     return result; */
            /* } */
        }

        public Task<ICollection<Cipher>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var results = await connection.QueryAsync<Cipher>( */
            /*         $"[{Schema}].[Cipher_ReadByOrganizationId]", */
            /*         new { OrganizationId = organizationId }, */
            /*         commandType: CommandType.StoredProcedure); */

            /*     return results.ToList(); */
            /* } */
        }

        public async Task<ICollection<CipherDetails>> GetManyByUserIdAsync(Guid userId, bool withOrganizations = true)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                IQueryable<CipherDetails> cipherDetailsView = new CipherDetailsQuery(userId).Run(dbContext);
                if (!withOrganizations)
                {
                    cipherDetailsView = from c in cipherDetailsView
                                        select new CipherDetails() {
                                            Id = c.Id,
                                            UserId = c.UserId,
                                            OrganizationId = c.OrganizationId,
                                            Type= c.Type,
                                            Data = c.Data,
                                            Attachments = c.Attachments,
                                            CreationDate = DateTime.UtcNow,
                                            RevisionDate = DateTime.UtcNow,
                                            DeletedDate = c.DeletedDate,
                                            Favorite = c.Favorite,
                                            FolderId = c.FolderId,
                                            Edit = true,
                                            ViewPassword = true,
                                            OrganizationUseTotp = false
                                        };
                }
                return await cipherDetailsView.ToListAsync();
            }
        }

        public Task<CipherOrganizationDetails> GetOrganizationDetailsByIdAsync(Guid id)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var results = await connection.QueryAsync<CipherDetails>( */
            /*         $"[{Schema}].[CipherOrganizationDetails_ReadById]", */
            /*         new { Id = id }, */
            /*         commandType: CommandType.StoredProcedure); */

            /*     return results.FirstOrDefault(); */
            /* } */
        }

        public Task MoveAsync(IEnumerable<Guid> ids, Guid? folderId, Guid userId)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var results = await connection.ExecuteAsync( */
            /*         $"[{Schema}].[Cipher_Move]", */
            /*         new { Ids = ids.ToGuidIdArrayTVP(), FolderId = folderId, UserId = userId }, */
            /*         commandType: CommandType.StoredProcedure); */
            /* } */
        }

        public async Task ReplaceAsync(CipherDetails cipher)
        {
            cipher.UserId = cipher.OrganizationId.HasValue ?
                null :
                cipher.UserId;
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entity = await dbContext.Ciphers.FindAsync(cipher.Id);
                if (entity != null)
                {
                    // TODO: Folders, Favorites
                    var mappedEntity = Mapper.Map<EfModel.Cipher>((TableModel.Cipher)cipher);
                    dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);
                    if (entity.OrganizationId.HasValue)
                    {
                        // TODO: User_BumpAccountRevisionDateByCipherId
                    }
                    else if (entity.UserId.HasValue)
                    {
                        // User_BumpAccountRevisionDate
                        var q = new UserBumpAccountRevisionDateByCipherId(cipher).Run(dbContext);
                        await q.ForEachAsync(u => u.RevisionDate = DateTime.UtcNow);
                    }
                    //
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        public async Task<bool> ReplaceAsync(Cipher obj, IEnumerable<Guid> collectionIds)
        {
            throw new NotImplementedException();
            /* var objWithCollections = JsonConvert.DeserializeObject<CipherWithCollections>( */
            /*     JsonConvert.SerializeObject(obj)); */
            /* objWithCollections.CollectionIds = collectionIds.ToGuidIdArrayTVP(); */

            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var result = await connection.ExecuteScalarAsync<int>( */
            /*         $"[{Schema}].[Cipher_UpdateWithCollections]", */
            /*         objWithCollections, */
            /*         commandType: CommandType.StoredProcedure); */
            /*     return result >= 0; */
            /* } */
        }

        public Task<DateTime> RestoreAsync(IEnumerable<Guid> ids, Guid userId)
        {
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var results = await connection.ExecuteScalarAsync<DateTime>( */
            /*         $"[{Schema}].[Cipher_Restore]", */
            /*         new { Ids = ids.ToGuidIdArrayTVP(), UserId = userId }, */
            /*         commandType: CommandType.StoredProcedure); */

            /*     return results; */
            /* } */
            throw new NotImplementedException();
        }

        public Task SoftDeleteAsync(IEnumerable<Guid> ids, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task SoftDeleteByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var results = await connection.ExecuteAsync( */
            /*         $"[{Schema}].[Cipher_SoftDeleteByIdsOrganizationId]", */
            /*         new { Ids = ids.ToGuidIdArrayTVP(), OrganizationId = organizationId }, */
            /*         commandType: CommandType.StoredProcedure); */
            /* } */
        }

        public Task UpdateAttachmentAsync(CipherAttachment attachment)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var results = await connection.ExecuteAsync( */
            /*         $"[{Schema}].[Cipher_UpdateAttachment]", */
            /*         attachment, */
            /*         commandType: CommandType.StoredProcedure); */
            /* } */
        }

        public Task UpdateCiphersAsync(Guid userId, IEnumerable<Cipher> ciphers)
        {
            throw new NotImplementedException();
            /* if (!ciphers.Any()) */
            /* { */
            /*     return; */
            /* } */

            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     connection.Open(); */

            /*     using (var transaction = connection.BeginTransaction()) */
            /*     { */
            /*         try */
            /*         { */
            /*             // 1. Create temp tables to bulk copy into. */

            /*             var sqlCreateTemp = @" */
            /*                 SELECT TOP 0 * */
            /*                 INTO #TempCipher */
            /*                 FROM [dbo].[Cipher]"; */

            /*             using (var cmd = new SqlCommand(sqlCreateTemp, connection, transaction)) */
            /*             { */
            /*                 cmd.ExecuteNonQuery(); */
            /*             } */

            /*             // 2. Bulk copy into temp tables. */
            /*             using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction)) */
            /*             { */
            /*                 bulkCopy.DestinationTableName = "#TempCipher"; */
            /*                 var dataTable = BuildCiphersTable(bulkCopy, ciphers); */
            /*                 bulkCopy.WriteToServer(dataTable); */
            /*             } */

            /*             // 3. Insert into real tables from temp tables and clean up. */

            /*             // Intentionally not including Favorites, Folders, and CreationDate */
            /*             // since those are not meant to be bulk updated at this time */
            /*             var sql = @" */
            /*                 UPDATE */
            /*                     [dbo].[Cipher] */
            /*                 SET */
            /*                     [UserId] = TC.[UserId], */
            /*                     [OrganizationId] = TC.[OrganizationId], */
            /*                     [Type] = TC.[Type], */
            /*                     [Data] = TC.[Data], */
            /*                     [Attachments] = TC.[Attachments], */
            /*                     [RevisionDate] = TC.[RevisionDate], */
            /*                     [DeletedDate] = TC.[DeletedDate] */
            /*                 FROM */
            /*                     [dbo].[Cipher] C */
            /*                 INNER JOIN */
            /*                     #TempCipher TC ON C.Id = TC.Id */
            /*                 WHERE */
            /*                     C.[UserId] = @UserId */

            /*                 DROP TABLE #TempCipher"; */

            /*             using (var cmd = new SqlCommand(sql, connection, transaction)) */
            /*             { */
            /*                 cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId; */
            /*                 cmd.ExecuteNonQuery(); */
            /*             } */

            /*             await connection.ExecuteAsync( */
            /*                 $"[{Schema}].[User_BumpAccountRevisionDate]", */
            /*                 new { Id = userId }, */
            /*                 commandType: CommandType.StoredProcedure, transaction: transaction); */

            /*             transaction.Commit(); */
            /*         } */
            /*         catch */
            /*         { */
            /*             transaction.Rollback(); */
            /*             throw; */
            /*         } */
            /*     } */
            /* } */
        }

        public Task UpdatePartialAsync(Guid id, Guid userId, Guid? folderId, bool favorite)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     var results = await connection.ExecuteAsync( */
            /*         $"[{Schema}].[Cipher_UpdatePartial]", */
            /*         new { Id = id, UserId = userId, FolderId = folderId, Favorite = favorite }, */
            /*         commandType: CommandType.StoredProcedure); */
            /* } */
        }

        public Task UpdateUserKeysAndCiphersAsync(User user, IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders)
        {
            throw new NotImplementedException();
            /* using (var connection = new SqlConnection(ConnectionString)) */
            /* { */
            /*     connection.Open(); */

            /*     using (var transaction = connection.BeginTransaction()) */
            /*     { */
            /*         try */
            /*         { */
            /*             // 1. Update user. */

            /*             using (var cmd = new SqlCommand("[dbo].[User_UpdateKeys]", connection, transaction)) */
            /*             { */
            /*                 cmd.CommandType = CommandType.StoredProcedure; */
            /*                 cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = user.Id; */
            /*                 cmd.Parameters.Add("@SecurityStamp", SqlDbType.NVarChar).Value = user.SecurityStamp; */
            /*                 cmd.Parameters.Add("@Key", SqlDbType.VarChar).Value = user.Key; */

            /*                 if (string.IsNullOrWhiteSpace(user.PrivateKey)) */
            /*                 { */
            /*                     cmd.Parameters.Add("@PrivateKey", SqlDbType.VarChar).Value = DBNull.Value; */
            /*                 } */
            /*                 else */
            /*                 { */
            /*                     cmd.Parameters.Add("@PrivateKey", SqlDbType.VarChar).Value = user.PrivateKey; */
            /*                 } */

            /*                 cmd.Parameters.Add("@RevisionDate", SqlDbType.DateTime2).Value = user.RevisionDate; */
            /*                 cmd.ExecuteNonQuery(); */
            /*             } */

            /*             // 2. Create temp tables to bulk copy into. */

            /*             var sqlCreateTemp = @" */
            /*                 SELECT TOP 0 * */
            /*                 INTO #TempCipher */
            /*                 FROM [dbo].[Cipher] */

            /*                 SELECT TOP 0 * */
            /*                 INTO #TempFolder */
            /*                 FROM [dbo].[Folder]"; */

            /*             using (var cmd = new SqlCommand(sqlCreateTemp, connection, transaction)) */
            /*             { */
            /*                 cmd.ExecuteNonQuery(); */
            /*             } */

            /*             // 3. Bulk copy into temp tables. */

            /*             if (ciphers.Any()) */
            /*             { */
            /*                 using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction)) */
            /*                 { */
            /*                     bulkCopy.DestinationTableName = "#TempCipher"; */
            /*                     var dataTable = BuildCiphersTable(bulkCopy, ciphers); */
            /*                     bulkCopy.WriteToServer(dataTable); */
            /*                 } */
            /*             } */

            /*             if (folders.Any()) */
            /*             { */
            /*                 using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction)) */
            /*                 { */
            /*                     bulkCopy.DestinationTableName = "#TempFolder"; */
            /*                     var dataTable = BuildFoldersTable(bulkCopy, folders); */
            /*                     bulkCopy.WriteToServer(dataTable); */
            /*                 } */
            /*             } */

            /*             // 4. Insert into real tables from temp tables and clean up. */

            /*             var sql = string.Empty; */

            /*             if (ciphers.Any()) */
            /*             { */
            /*                 sql += @" */
            /*                     UPDATE */
            /*                         [dbo].[Cipher] */
            /*                     SET */
            /*                         [Data] = TC.[Data], */
            /*                         [Attachments] = TC.[Attachments], */
            /*                         [RevisionDate] = TC.[RevisionDate] */
            /*                     FROM */
            /*                         [dbo].[Cipher] C */
            /*                     INNER JOIN */
            /*                         #TempCipher TC ON C.Id = TC.Id */
            /*                     WHERE */
            /*                         C.[UserId] = @UserId"; */
            /*             } */

            /*             if (folders.Any()) */
            /*             { */
            /*                 sql += @" */
            /*                     UPDATE */
            /*                         [dbo].[Folder] */
            /*                     SET */
            /*                         [Name] = TF.[Name], */
            /*                         [RevisionDate] = TF.[RevisionDate] */
            /*                     FROM */
            /*                         [dbo].[Folder] F */
            /*                     INNER JOIN */
            /*                         #TempFolder TF ON F.Id = TF.Id */
            /*                     WHERE */
            /*                         F.[UserId] = @UserId"; */
            /*             } */

            /*             sql += @" */
            /*                 DROP TABLE #TempCipher */
            /*                 DROP TABLE #TempFolder"; */

            /*             using (var cmd = new SqlCommand(sql, connection, transaction)) */
            /*             { */
            /*                 cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = user.Id; */
            /*                 cmd.ExecuteNonQuery(); */
            /*             } */

            /*             transaction.Commit(); */
            /*         } */
            /*         catch */
            /*         { */
            /*             transaction.Rollback(); */
            /*             throw; */
            /*         } */
            /*     } */
            /* } */
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

        public Task DeleteDeletedAsync(DateTime deletedDateBefore)
        {
            throw new NotImplementedException();
        }
    }
}

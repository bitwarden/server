using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework.Queries;
using Bit.Core.Utilities;
using Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using TableModel = Bit.Core.Models.Table;

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
                    await UserBumpAccountRevisionDateByCipherId(cipher);
                }
                else if (cipher.UserId.HasValue)
                {
                    await UserBumpAccountRevisionDate(cipher.UserId.Value);
                }
            }
            return cipher;
        }

        public IQueryable<User> GetBumpedAccountsByCipherId(Cipher cipher)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = new UserBumpAccountRevisionDateByCipherIdQuery(cipher);
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
                var cipherEntity = await dbContext.Ciphers.FindAsync(cipher.Id);
                var query = new CipherUpdateCollectionsQuery(cipherEntity, collectionIds).Run(dbContext);
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
                var userIdKey = $"\"{cipher.UserId}\"";
                cipher.UserId = cipher.OrganizationId.HasValue ? null : cipher.UserId;
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
            await UserBumpAccountRevisionDateByCipherId(cipher);
            return cipher;
        }

        public async Task CreateAsync(CipherDetails cipher, IEnumerable<Guid> collectionIds)
        {
            cipher = await CreateAsyncReturnCipher(cipher);
            await UpdateCollections(cipher, collectionIds);
        }

        public async Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders)
        {
            if (!ciphers.Any())
            {
                return;
            }

            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var folderEntities = Mapper.Map<List<EfModel.Folder>>(folders);
                await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, folderEntities);
                var cipherEntities = Mapper.Map<List<EfModel.Cipher>>(ciphers);
                await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, cipherEntities);
                await UserBumpAccountRevisionDateByCipherId(ciphers);
            }
        }

        public async Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Collection> collections, IEnumerable<CollectionCipher> collectionCiphers)
        {
            if (!ciphers.Any()) 
            {
                return; 
            }
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                // Intentionally not including Favorites, Folders, and CreationDate */
                // since those are not meant to be bulk updated at this time */
                var cipherEntities = Mapper.Map<List<EfModel.Cipher>>(ciphers);
                await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, cipherEntities);
                if (collections.Any())
                {
                    var collectionEntities = Mapper.Map<List<EfModel.Collection>>(collections);
                    await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, collectionEntities);

                    if (collectionCiphers.Any())
                    {
                        var collectionCipherEntities = Mapper.Map<List<EfModel.CollectionCipher>>(collectionCiphers);
                        await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, collectionCipherEntities);
                    }
                }
                await UserBumpAccountRevisionDateByOrganizationId(ciphers.FirstOrDefault().OrganizationId.Value);
            }
        }

        public async Task DeleteAsync(IEnumerable<Guid> ids, Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var idEntities = from c in dbContext.Ciphers where ids.Contains(c.Id) select c;
                var userCipherDetails = new UserCipherDetailsQuery(userId).Run(dbContext);
                var temp = from ucd in userCipherDetails 
                           join ie in idEntities 
                                on ucd.Id equals ie.Id 
                           where ucd.Edit
                           select ucd;
                dbContext.RemoveRange(temp.Select(ucd => ucd.Id));
                var orgs = temp
                    .Where(x => x.OrganizationId.HasValue)
                    .GroupBy(x => x.OrganizationId).Select(x => x.Key);
                foreach (var org in orgs)
                {
                    await OrganizationUpdateStorage(org.Value);
                    await UserBumpAccountRevisionDateByOrganizationId(org.Value);
                }
                if (await temp.AnyAsync(x => x.UserId.HasValue && !string.IsNullOrWhiteSpace(x.Attachments)))
                {
                    await UserUpdateStorage(userId);
                }
                await dbContext.SaveChangesAsync();
                await UserBumpAccountRevisionDate(userId);
            }
        }

        public async Task DeleteAttachmentAsync(Guid cipherId, string attachmentId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var cipher = await dbContext.Ciphers.FindAsync(cipherId);
                var attachmentsJson = JObject.Parse(cipher.Attachments);
                attachmentsJson.Remove(attachmentId);
                cipher.Attachments = JsonConvert.SerializeObject(attachmentsJson);
                await dbContext.SaveChangesAsync();

                if (cipher.OrganizationId.HasValue)
                {
                    await OrganizationUpdateStorage(cipher.OrganizationId.Value);
                    await UserBumpAccountRevisionDateByCipherId(cipher);
                }
                else if (cipher.UserId.HasValue)
                {
                    await UserUpdateStorage(cipher.UserId.Value);
                    await UserBumpAccountRevisionDate(cipher.UserId.Value);
                }
            }
        }

        public async Task DeleteByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var ciphers = from c in dbContext.Ciphers
                              where c.OrganizationId == organizationId &&
                                    ids.Contains(c.Id)
                              select c;
                dbContext.RemoveRange(ciphers);
                await dbContext.SaveChangesAsync();
            }
            await OrganizationUpdateStorage(organizationId);
            await UserBumpAccountRevisionDateByOrganizationId(organizationId);
        }

        public async Task DeleteByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);

                var collectionCiphers = from cc in dbContext.CollectionCiphers
                                        join c in dbContext.Collections
                                            on cc.CollectionId equals c.Id
                                        where c.OrganizationId == organizationId
                                        select new { cc, c };
                dbContext.RemoveRange(collectionCiphers.Select(x => x.cc));

                var ciphers = from c in dbContext.Ciphers
                              where c.OrganizationId == organizationId
                              select c;
                dbContext.RemoveRange(ciphers);

                await dbContext.SaveChangesAsync();
            }
            await OrganizationUpdateStorage(organizationId);
            await UserBumpAccountRevisionDateByOrganizationId(organizationId);
        }

        public async Task DeleteByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var ciphers = from c in dbContext.Ciphers
                              where c.UserId == userId
                              select c;
                dbContext.RemoveRange(ciphers);
                var folders = from f in dbContext.Folders
                              where f.UserId == userId
                              select f;
                dbContext.RemoveRange(folders);
                await dbContext.SaveChangesAsync();
                await UserUpdateStorage(userId);
                await UserBumpAccountRevisionDate(userId);
            }

        }

        public async Task DeleteDeletedAsync(DateTime deletedDateBefore)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = dbContext.Ciphers.Where(c => c.DeletedDate < deletedDateBefore);
                dbContext.RemoveRange(query);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<CipherDetails> GetByIdAsync(Guid id, Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var userCipherDetails = new UserCipherDetailsQuery(userId);
                var data = await userCipherDetails.Run(dbContext).FirstOrDefaultAsync(c => c.Id == id);
                return data;
            }
        }

        public async Task<bool> GetCanEditByIdAsync(Guid userId, Guid cipherId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = new CipherReadCanEditByIdUserIdQuery(userId, cipherId);
                var canEdit = await query.Run(dbContext).AnyAsync();
                return canEdit;
            }
        }

        public async Task<ICollection<Cipher>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = dbContext.Ciphers.Where(x => !x.UserId.HasValue && x.OrganizationId == organizationId);
                var data = await query.ToListAsync();
                return Mapper.Map<List<TableModel.Cipher>>(data);
            }
        }

        public async Task<ICollection<CipherDetails>> GetManyByUserIdAsync(Guid userId, bool withOrganizations = true)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                IQueryable<CipherDetails> cipherDetailsView = withOrganizations ? 
                    new UserCipherDetailsQuery(userId).Run(dbContext) :
                    new CipherDetailsQuery(userId).Run(dbContext);
                if (!withOrganizations)
                {
                    cipherDetailsView = from c in cipherDetailsView
                                        where c.UserId == userId
                                        select new CipherDetails() {
                                            Id = c.Id,
                                            UserId = c.UserId,
                                            OrganizationId = c.OrganizationId,
                                            Type= c.Type,
                                            Data = c.Data,
                                            Attachments = c.Attachments,
                                            CreationDate = c.CreationDate,
                                            RevisionDate = c.RevisionDate,
                                            DeletedDate = c.DeletedDate,
                                            Favorite = c.Favorite,
                                            FolderId = c.FolderId,
                                            Edit = true,
                                            ViewPassword = true,
                                            OrganizationUseTotp = false
                                        };
                }
                var ciphers = await cipherDetailsView.ToListAsync();
                return ciphers;
            }
        }

        public async Task<CipherOrganizationDetails> GetOrganizationDetailsByIdAsync(Guid id)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = new CipherOrganizationDetailsReadByIdQuery(id);
                var data = await query.Run(dbContext).FirstOrDefaultAsync();
                return data;
            }
        }

        public async Task MoveAsync(IEnumerable<Guid> ids, Guid? folderId, Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var cipherEntities = dbContext.Ciphers.Where(c => ids.Contains(c.Id));
                var userCipherDetails = new UserCipherDetailsQuery(userId).Run(dbContext);
                var idsToMove = from ucd in userCipherDetails
                                join c in cipherEntities
                                    on ucd.Id equals c.Id
                                where ucd.Edit
                                select new { ucd, c };
                await idsToMove.Select(x => x.c).ForEachAsync(cipher => {
                    var foldersJson = JObject.Parse(cipher.Folders);
                    foldersJson.Remove(userId.ToString());
                    dbContext.Attach(cipher);
                    cipher.Folders = JsonConvert.SerializeObject(foldersJson);
                });          
                await UserBumpAccountRevisionDate(userId);
                await dbContext.SaveChangesAsync();
            }
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
                    var userIdKey = $"\"{cipher.UserId}\"";
                    if (cipher.Favorite)
                    {
                        if (cipher.Favorites == null)
                        {
                            cipher.Favorites = $"{{{userIdKey}:true}}";
                        }
                        else
                        {
                            var favorites = CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, bool>>(cipher.Favorites);
                            favorites.Add(cipher.UserId.Value, true);
                            cipher.Favorites = JsonConvert.SerializeObject(favorites);
                        }
                    }
                    else
                    {
                        if (cipher.Favorites != null && cipher.Favorites.Contains(cipher.UserId.Value.ToString()))
                        {
                            var favorites = CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, bool>>(cipher.Favorites);
                            favorites.Remove(cipher.UserId.Value);
                            cipher.Favorites = JsonConvert.SerializeObject(favorites);
                        }
                    }
                    if (cipher.FolderId.HasValue)
                    {
                        if (cipher.Folders == null)
                        {
                            cipher.Folders = $"{{{userIdKey}:\"{cipher.FolderId}\"}}";
                        }
                        else
                        {
                            var folders = CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, Guid>>(cipher.Folders);
                            folders.Add(cipher.UserId.Value, cipher.FolderId.Value);
                            cipher.Folders = JsonConvert.SerializeObject(folders);
                        }
                    }
                    else
                    {
                        if (cipher.Folders != null && cipher.Folders.Contains(cipher.UserId.Value.ToString()))
                        {
                            var folders = CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, bool>>(cipher.Favorites);
                            folders.Remove(cipher.UserId.Value);
                            cipher.Favorites = JsonConvert.SerializeObject(folders);
                        }
                    }
                    var mappedEntity = Mapper.Map<EfModel.Cipher>((TableModel.Cipher)cipher);
                    dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);
                    await UserBumpAccountRevisionDateByCipherId(cipher);
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        public async Task<bool> ReplaceAsync(Cipher obj, IEnumerable<Guid> collectionIds)
        {
            await UpdateCollections(obj, collectionIds);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var cipher = await dbContext.Ciphers.FindAsync(obj.Id);
                cipher.UserId = null;
                cipher.OrganizationId = obj.OrganizationId;
                cipher.Data = obj.Data;
                cipher.Attachments = obj.Attachments;
                cipher.RevisionDate = obj.RevisionDate;
                cipher.DeletedDate = obj.DeletedDate;
                await dbContext.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(cipher.Attachments))
                {
                    if (cipher.OrganizationId.HasValue)
                    {
                        await OrganizationUpdateStorage(cipher.OrganizationId.Value);
                    }
                    else if (cipher.UserId.HasValue)
                    {
                        await UserUpdateStorage(cipher.UserId.Value);
                    }
                }

                await UserBumpAccountRevisionDateByCipherId(cipher);
                return true;
            }
        }

        public async Task<DateTime> RestoreAsync(IEnumerable<Guid> ids, Guid userId)
        {
            return await ToggleCipherStates(ids, userId, false);
        }

        public async Task SoftDeleteAsync(IEnumerable<Guid> ids, Guid userId)
        {
            await ToggleCipherStates(ids, userId, false);
        }

        private async Task<DateTime> ToggleCipherStates(IEnumerable<Guid> ids, Guid userId, bool isRestore)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var userCipherDetailsQuery = new UserCipherDetailsQuery(userId);
                var cipherEntities = dbContext.Ciphers.Where(c => ids.Contains(c.Id));
                var temp = from ucd in userCipherDetailsQuery.Run(dbContext)
                           join c in cipherEntities
                            on ucd.Id equals c.Id
                           where ucd.Edit && ucd.DeletedDate == null
                           select new { ucd, c };

                var utcNow = DateTime.UtcNow;
                await temp.Select(x => x.c).ForEachAsync(cipher => {
                    cipher.DeletedDate = isRestore ? null : utcNow;
                    cipher.RevisionDate = utcNow;
                });

                var orgIds = temp
                    .Where(x => x.c.OrganizationId.HasValue)
                    .GroupBy(x => x.c.OrganizationId).Select(x => x.Key);

                foreach (var orgId in orgIds)
                {
                    await OrganizationUpdateStorage(orgId.Value);
                    await UserBumpAccountRevisionDateByOrganizationId(orgId.Value);
                }
                if (await temp.AnyAsync(x => x.c.UserId.HasValue && !string.IsNullOrWhiteSpace(x.c.Attachments)))
                {
                    await UserUpdateStorage(userId);
                }
                await UserBumpAccountRevisionDate(userId);
                await dbContext.SaveChangesAsync();
                return utcNow;
            }
        }

        public async Task SoftDeleteByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var utcNow = DateTime.UtcNow;
                var ciphers = dbContext.Ciphers.Where(c => ids.Contains(c.Id) && c.OrganizationId == organizationId);
                await ciphers.ForEachAsync(cipher => {
                    dbContext.Attach(cipher);
                    cipher.DeletedDate = utcNow;
                    cipher.RevisionDate = utcNow;
                }); 
                await dbContext.SaveChangesAsync();
                await OrganizationUpdateStorage(organizationId);
                await UserBumpAccountRevisionDateByOrganizationId(organizationId);
            }
        }

        public async Task UpdateAttachmentAsync(CipherAttachment attachment)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var cipher = await dbContext.Ciphers.FindAsync(attachment.Id);
                var attachmentsJson = string.IsNullOrWhiteSpace(cipher.Attachments) ? new JObject() : JObject.Parse(cipher.Attachments);
                attachmentsJson.Add(attachment.AttachmentId, attachment.AttachmentData);
                cipher.Attachments = JsonConvert.SerializeObject(attachmentsJson);
                await dbContext.SaveChangesAsync();

                if (attachment.OrganizationId.HasValue)
                {
                    await OrganizationUpdateStorage(cipher.OrganizationId.Value);
                    await UserBumpAccountRevisionDateByCipherId(new List<Cipher> { cipher });
                }
                else if (attachment.UserId.HasValue)
                {
                    await UserUpdateStorage(attachment.UserId.Value);
                    await UserBumpAccountRevisionDate(attachment.UserId.Value);
                }
            }
        }

        public async Task UpdateCiphersAsync(Guid userId, IEnumerable<Cipher> ciphers)
        {
            if (!ciphers.Any()) 
            {
                return; 
            }
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entities = Mapper.Map<List<EfModel.Cipher>>(ciphers);
                await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, entities);
                await UserBumpAccountRevisionDate(userId);
            }
        }

        public async Task UpdatePartialAsync(Guid id, Guid userId, Guid? folderId, bool favorite)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var cipher = await dbContext.Ciphers.FindAsync(id);

                var foldersJson = JObject.Parse(cipher.Folders);
                if (foldersJson == null && folderId.HasValue)
                {
                    foldersJson.Add(userId.ToString(), folderId.Value);
                }
                else if (foldersJson != null && folderId.HasValue)
                {
                    foldersJson[userId] = folderId.Value;
                }
                else 
                {
                    foldersJson.Remove(userId.ToString());
                }

                var favoritesJson = JObject.Parse(cipher.Favorites);
                if (favorite)
                {
                    favoritesJson.Add(userId.ToString(), favorite);
                }
                else 
                {
                    favoritesJson.Remove(userId.ToString());
                }

                await dbContext.SaveChangesAsync();
                await UserBumpAccountRevisionDate(userId);
            }
        }

        public async Task UpdateUserKeysAndCiphersAsync(User user, IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                await UserUpdateKeys(user);
                var cipherEntities = Mapper.Map<List<EfModel.Cipher>>(ciphers);
                await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, cipherEntities);
                var folderEntities = Mapper.Map<List<EfModel.Folder>>(folders);
                await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, folderEntities);
                await dbContext.SaveChangesAsync();
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

    }
}

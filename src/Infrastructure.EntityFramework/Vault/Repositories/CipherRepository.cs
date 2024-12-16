﻿using System.Text.Json;
using System.Text.Json.Nodes;
using AutoMapper;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Utilities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Bit.Infrastructure.EntityFramework.Repositories.Vault.Queries;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Bit.Infrastructure.EntityFramework.Vault.Repositories.Queries;
using LinqToDB.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NS = Newtonsoft.Json;
using NSL = Newtonsoft.Json.Linq;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories;

public class CipherRepository : Repository<Core.Vault.Entities.Cipher, Cipher, Guid>, ICipherRepository
{
    public CipherRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Ciphers)
    { }

    public override async Task<Core.Vault.Entities.Cipher> CreateAsync(Core.Vault.Entities.Cipher cipher)
    {
        cipher = await base.CreateAsync(cipher);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            if (cipher.OrganizationId.HasValue)
            {
                await dbContext.UserBumpAccountRevisionDateByCipherIdAsync(cipher.Id, cipher.OrganizationId.Value);
            }
            else if (cipher.UserId.HasValue)
            {
                await dbContext.UserBumpAccountRevisionDateAsync(cipher.UserId.Value);
            }
            await dbContext.SaveChangesAsync();
        }
        return cipher;
    }

    public override async Task DeleteAsync(Core.Vault.Entities.Cipher cipher)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var cipherInfo = await dbContext.Ciphers
                .Where(c => c.Id == cipher.Id)
                .Select(c => new { c.UserId, c.OrganizationId, HasAttachments = c.Attachments != null })
                .FirstOrDefaultAsync();

            await base.DeleteAsync(cipher);

            if (cipherInfo?.OrganizationId != null)
            {
                if (cipherInfo.HasAttachments == true)
                {
                    await OrganizationUpdateStorage(cipherInfo.OrganizationId.Value);
                }

                await dbContext.UserBumpAccountRevisionDateByCipherIdAsync(cipher.Id, cipherInfo.OrganizationId.Value);
            }
            else if (cipherInfo?.UserId != null)
            {
                if (cipherInfo.HasAttachments)
                {
                    await UserUpdateStorage(cipherInfo.UserId.Value);
                }

                await dbContext.UserBumpAccountRevisionDateAsync(cipherInfo.UserId.Value);
            }
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task CreateAsync(Core.Vault.Entities.Cipher cipher, IEnumerable<Guid> collectionIds)
    {
        cipher = await CreateAsync(cipher);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await UpdateCollectionsAsync(dbContext, cipher.Id,
                cipher.UserId, cipher.OrganizationId, collectionIds);
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
            var entity = Mapper.Map<Cipher>((Core.Vault.Entities.Cipher)cipher);
            await dbContext.AddAsync(entity);

            await dbContext.SaveChangesAsync();

            if (cipher.OrganizationId.HasValue)
            {
                await dbContext.UserBumpAccountRevisionDateByCipherIdAsync(cipher.Id, cipher.OrganizationId.Value);
            }
            else if (cipher.UserId.HasValue)
            {
                await dbContext.UserBumpAccountRevisionDateAsync(cipher.UserId.Value);
            }

            await dbContext.SaveChangesAsync();
        }
        return cipher;
    }

    public async Task CreateAsync(CipherDetails cipher, IEnumerable<Guid> collectionIds)
    {
        cipher = await CreateAsyncReturnCipher(cipher);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await UpdateCollectionsAsync(dbContext, cipher.Id,
                cipher.UserId, cipher.OrganizationId, collectionIds);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task CreateAsync(IEnumerable<Core.Vault.Entities.Cipher> ciphers, IEnumerable<Core.Vault.Entities.Folder> folders)
    {
        if (!ciphers.Any())
        {
            return;
        }

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var folderEntities = Mapper.Map<List<Folder>>(folders);
            await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, folderEntities);
            var cipherEntities = Mapper.Map<List<Cipher>>(ciphers);
            await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, cipherEntities);
            await dbContext.UserBumpAccountRevisionDateAsync(ciphers.First().UserId.GetValueOrDefault());
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task CreateAsync(IEnumerable<Core.Vault.Entities.Cipher> ciphers,
        IEnumerable<Core.Entities.Collection> collections,
        IEnumerable<Core.Entities.CollectionCipher> collectionCiphers,
        IEnumerable<Core.Entities.CollectionUser> collectionUsers)
    {
        if (!ciphers.Any())
        {
            return;
        }
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var cipherEntities = Mapper.Map<List<Cipher>>(ciphers);
            await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, cipherEntities);

            if (collections.Any())
            {
                var collectionEntities = Mapper.Map<List<Collection>>(collections);
                await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, collectionEntities);
            }

            if (collectionCiphers.Any())
            {
                var collectionCipherEntities = Mapper.Map<List<CollectionCipher>>(collectionCiphers);
                await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, collectionCipherEntities);
            }

            if (collectionUsers.Any())
            {
                var collectionUserEntities = Mapper.Map<List<CollectionUser>>(collectionUsers);
                await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, collectionUserEntities);
            }

            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(ciphers.First().OrganizationId.Value);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(IEnumerable<Guid> ids, Guid userId)
    {
        await ToggleCipherStates(ids, userId, CipherStateAction.HardDelete);
    }

    public async Task DeleteAttachmentAsync(Guid cipherId, string attachmentId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var cipher = await dbContext.Ciphers.FindAsync(cipherId);
            var attachmentsJson = NSL.JObject.Parse(cipher.Attachments);
            attachmentsJson.Remove(attachmentId);
            cipher.Attachments = NS.JsonConvert.SerializeObject(attachmentsJson);
            await dbContext.SaveChangesAsync();

            if (cipher.OrganizationId.HasValue)
            {
                await OrganizationUpdateStorage(cipher.OrganizationId.Value);
                await dbContext.UserBumpAccountRevisionDateByCipherIdAsync(cipher.Id, cipher.OrganizationId.Value);
            }
            else if (cipher.UserId.HasValue)
            {
                await UserUpdateStorage(cipher.UserId.Value);
                await dbContext.UserBumpAccountRevisionDateAsync(cipher.UserId.Value);
            }
            await dbContext.SaveChangesAsync();
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
            await OrganizationUpdateStorage(organizationId);
            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(organizationId);
            await dbContext.SaveChangesAsync();
        }
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
                                    select cc;
            dbContext.RemoveRange(collectionCiphers);

            var ciphers = from c in dbContext.Ciphers
                          where c.OrganizationId == organizationId
                          select c;
            dbContext.RemoveRange(ciphers);

            await OrganizationUpdateStorage(organizationId);
            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(organizationId);
            await dbContext.SaveChangesAsync();
        }
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
            await dbContext.UserBumpAccountRevisionDateAsync(userId);
            await dbContext.SaveChangesAsync();
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

    public async Task<ICollection<CipherOrganizationDetails>> GetManyOrganizationDetailsByOrganizationIdAsync(
        Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new CipherOrganizationDetailsReadByOrganizationIdQuery(organizationId);
            var data = await query.Run(dbContext).ToListAsync();
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

    public async Task<ICollection<Core.Vault.Entities.Cipher>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.Ciphers.Where(x => !x.UserId.HasValue && x.OrganizationId == organizationId);
            var data = await query.ToListAsync();
            return Mapper.Map<List<Core.Vault.Entities.Cipher>>(data);
        }
    }

    public async Task<ICollection<CipherOrganizationDetails>> GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new CipherOrganizationDetailsReadByOrganizationIdQuery(organizationId, true);
            var data = await query.Run(dbContext).ToListAsync();
            return data;
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
                                    select new CipherDetails
                                    {
                                        Id = c.Id,
                                        UserId = c.UserId,
                                        OrganizationId = c.OrganizationId,
                                        Type = c.Type,
                                        Data = c.Data,
                                        Attachments = c.Attachments,
                                        CreationDate = c.CreationDate,
                                        RevisionDate = c.RevisionDate,
                                        DeletedDate = c.DeletedDate,
                                        Favorite = c.Favorite,
                                        FolderId = c.FolderId,
                                        Edit = true,
                                        Reprompt = c.Reprompt,
                                        ViewPassword = true,
                                        OrganizationUseTotp = false,
                                        Key = c.Key
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
                            select c;
            await idsToMove.ForEachAsync(cipher =>
            {
                var foldersJson = string.IsNullOrWhiteSpace(cipher.Folders) ?
                    new NSL.JObject() :
                    NSL.JObject.Parse(cipher.Folders);

                if (folderId.HasValue)
                {
                    foldersJson.Remove(userId.ToString());
                    foldersJson.Add(userId.ToString(), folderId.Value.ToString());
                }
                else if (!string.IsNullOrWhiteSpace(cipher.Folders))
                {
                    foldersJson.Remove(userId.ToString());
                }
                dbContext.Attach(cipher);
                cipher.Folders = NS.JsonConvert.SerializeObject(foldersJson);
            });
            await dbContext.UserBumpAccountRevisionDateAsync(userId);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task ReplaceAsync(CipherDetails cipher)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await dbContext.Ciphers.FindAsync(cipher.Id);
            if (entity != null)
            {
                if (cipher.Favorite)
                {
                    if (cipher.Favorites == null)
                    {
                        var jsonObject = new JsonObject(new[]
                        {
                            new KeyValuePair<string, JsonNode>(cipher.UserId.Value.ToString(), true),
                        });
                        cipher.Favorites = JsonSerializer.Serialize(jsonObject);
                    }
                    else
                    {
                        var favorites = CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, bool>>(cipher.Favorites);
                        favorites.Add(cipher.UserId.Value, true);
                        cipher.Favorites = JsonSerializer.Serialize(favorites);
                    }
                }
                else
                {
                    if (cipher.Favorites != null && cipher.Favorites.Contains(cipher.UserId.Value.ToString()))
                    {
                        var favorites = CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, bool>>(cipher.Favorites);
                        favorites.Remove(cipher.UserId.Value);
                        cipher.Favorites = JsonSerializer.Serialize(favorites);
                    }
                }
                if (cipher.FolderId.HasValue)
                {
                    if (cipher.Folders == null)
                    {
                        var jsonObject = new JsonObject(new[]
                        {
                            new KeyValuePair<string, JsonNode>(cipher.UserId.Value.ToString(), cipher.FolderId),
                        });
                        cipher.Folders = JsonSerializer.Serialize(jsonObject);
                    }
                    else
                    {
                        var folders = CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, Guid>>(cipher.Folders);
                        folders.Add(cipher.UserId.Value, cipher.FolderId.Value);
                        cipher.Folders = JsonSerializer.Serialize(folders);
                    }
                }
                else
                {
                    if (cipher.Folders != null && cipher.Folders.Contains(cipher.UserId.Value.ToString()))
                    {
                        var folders = CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, Guid>>(cipher.Folders);
                        folders.Remove(cipher.UserId.Value);
                        cipher.Folders = JsonSerializer.Serialize(folders);
                    }
                }

                // Check if this cipher is a part of an organization, and if so do
                // not save the UserId into the database. This must be done after we
                // set the user specific data like Folders and Favorites because
                // the UserId key is used for that
                cipher.UserId = cipher.OrganizationId.HasValue ?
                    null :
                    cipher.UserId;

                var mappedEntity = Mapper.Map<Cipher>(cipher);
                dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);

                if (cipher.OrganizationId.HasValue)
                {
                    await dbContext.UserBumpAccountRevisionDateByCipherIdAsync(cipher.Id, cipher.OrganizationId.Value);
                }
                else if (cipher.UserId.HasValue)
                {
                    await dbContext.UserBumpAccountRevisionDateAsync(cipher.UserId.Value);
                }

                await dbContext.SaveChangesAsync();
            }
        }
    }

    private static async Task<int> UpdateCollectionsAsync(DatabaseContext context, Guid id, Guid? userId, Guid? organizationId, IEnumerable<Guid> collectionIds)
    {
        if (!organizationId.HasValue || !collectionIds.Any())
        {
            return -1;
        }

        IQueryable<Guid> availableCollectionsQuery;

        if (!userId.HasValue)
        {
            availableCollectionsQuery = context.Collections
                .Where(c => c.OrganizationId == organizationId.Value)
                .Select(c => c.Id);
        }
        else
        {
            availableCollectionsQuery =
                new CollectionsReadByOrganizationIdUserIdQuery(organizationId.Value, userId.Value)
                    .Run(context)
                    .Select(c => c.Id);
        }

        var availableCollections = await availableCollectionsQuery.ToListAsync();

        if (!availableCollections.Any())
        {
            return -1;
        }

        var collectionCiphers = collectionIds
            .Where(collectionId => availableCollections.Contains(collectionId))
            .Select(collectionId => new CollectionCipher
            {
                CollectionId = collectionId,
                CipherId = id,
            });
        context.CollectionCiphers.AddRange(collectionCiphers);
        return 0;
    }

    public async Task<bool> ReplaceAsync(Core.Vault.Entities.Cipher cipher, IEnumerable<Guid> collectionIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var transaction = await dbContext.Database.BeginTransactionAsync();
            var successes = await UpdateCollectionsAsync(
                dbContext, cipher.Id, cipher.UserId,
                cipher.OrganizationId, collectionIds);

            if (successes < 0)
            {
                await transaction.CommitAsync();
                return false;
            }

            var trackedCipher = await dbContext.Ciphers.FindAsync(cipher.Id);

            trackedCipher.UserId = null;
            trackedCipher.OrganizationId = cipher.OrganizationId;
            trackedCipher.Data = cipher.Data;
            trackedCipher.Attachments = cipher.Attachments;
            trackedCipher.RevisionDate = cipher.RevisionDate;
            trackedCipher.DeletedDate = cipher.DeletedDate;
            trackedCipher.Key = cipher.Key;

            await transaction.CommitAsync();

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

            if (cipher.OrganizationId.HasValue)
            {
                await dbContext.UserBumpAccountRevisionDateByCipherIdAsync(cipher.Id, cipher.OrganizationId.Value);
            }
            else if (cipher.UserId.HasValue)
            {
                await dbContext.UserBumpAccountRevisionDateAsync(cipher.UserId.Value);
            }

            await dbContext.SaveChangesAsync();
            return true;
        }
    }

    public async Task<DateTime> RestoreAsync(IEnumerable<Guid> ids, Guid userId)
    {
        return await ToggleCipherStates(ids, userId, CipherStateAction.Restore);
    }

    public async Task<DateTime> RestoreByIdsOrganizationIdAsync(IEnumerable<Guid> ids, Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var utcNow = DateTime.UtcNow;
            var ciphers = from c in dbContext.Ciphers
                          where c.OrganizationId == organizationId &&
                                ids.Contains(c.Id)
                          select c;

            await ciphers.ForEachAsync(cipher =>
            {
                dbContext.Attach(cipher);
                cipher.DeletedDate = null;
                cipher.RevisionDate = utcNow;
            });

            await OrganizationUpdateStorage(organizationId);
            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(organizationId);
            await dbContext.SaveChangesAsync();
            return utcNow;
        }
    }

    public async Task SoftDeleteAsync(IEnumerable<Guid> ids, Guid userId)
    {
        await ToggleCipherStates(ids, userId, CipherStateAction.SoftDelete);
    }

    private async Task<DateTime> ToggleCipherStates(IEnumerable<Guid> ids, Guid userId, CipherStateAction action)
    {
        static bool FilterDeletedDate(CipherStateAction action, CipherDetails ucd)
        {
            return action switch
            {
                CipherStateAction.Restore => ucd.DeletedDate != null,
                CipherStateAction.SoftDelete => ucd.DeletedDate == null,
                _ => true,
            };
        }

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var userCipherDetailsQuery = new UserCipherDetailsQuery(userId);
            var cipherEntitiesToCheck = await (dbContext.Ciphers.Where(c => ids.Contains(c.Id))).ToListAsync();
            var query = from ucd in await (userCipherDetailsQuery.Run(dbContext)).ToListAsync()
                        join c in cipherEntitiesToCheck
                            on ucd.Id equals c.Id
                        where ucd.Edit && FilterDeletedDate(action, ucd)
                        select c;

            var utcNow = DateTime.UtcNow;
            var cipherIdsToModify = query.Select(c => c.Id);
            var cipherEntitiesToModify = dbContext.Ciphers.Where(x => cipherIdsToModify.Contains(x.Id));
            if (action == CipherStateAction.HardDelete)
            {
                dbContext.RemoveRange(cipherEntitiesToModify);
            }
            else
            {
                await cipherEntitiesToModify.ForEachAsync(cipher =>
                {
                    dbContext.Attach(cipher);
                    cipher.DeletedDate = action == CipherStateAction.Restore ? null : utcNow;
                    cipher.RevisionDate = utcNow;
                });
            }

            var orgIds = query
                .Where(c => c.OrganizationId.HasValue)
                .GroupBy(c => c.OrganizationId).Select(x => x.Key);

            foreach (var orgId in orgIds)
            {
                await OrganizationUpdateStorage(orgId.Value);
                await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(orgId.Value);
            }
            if (query.Any(c => c.UserId.HasValue && !string.IsNullOrWhiteSpace(c.Attachments)))
            {
                await UserUpdateStorage(userId);
            }
            await dbContext.UserBumpAccountRevisionDateAsync(userId);
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
            await ciphers.ForEachAsync(cipher =>
            {
                dbContext.Attach(cipher);
                cipher.DeletedDate = utcNow;
                cipher.RevisionDate = utcNow;
            });
            await OrganizationUpdateStorage(organizationId);
            await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(organizationId);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task UpdateAttachmentAsync(CipherAttachment attachment)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var cipher = await dbContext.Ciphers.FindAsync(attachment.Id);
            var attachments = string.IsNullOrWhiteSpace(cipher.Attachments) ?
                new Dictionary<string, CipherAttachment.MetaData>() :
                NS.JsonConvert.DeserializeObject<Dictionary<string, CipherAttachment.MetaData>>(cipher.Attachments);
            var metaData = NS.JsonConvert.DeserializeObject<CipherAttachment.MetaData>(attachment.AttachmentData);
            attachments[attachment.AttachmentId] = metaData;
            cipher.Attachments = NS.JsonConvert.SerializeObject(attachments);
            await dbContext.SaveChangesAsync();

            if (attachment.OrganizationId.HasValue)
            {
                await OrganizationUpdateStorage(cipher.OrganizationId.Value);
                await dbContext.UserBumpAccountRevisionDateByCipherIdAsync(cipher.Id, cipher.OrganizationId.Value);
            }
            else if (attachment.UserId.HasValue)
            {
                await UserUpdateStorage(attachment.UserId.Value);
                await dbContext.UserBumpAccountRevisionDateAsync(attachment.UserId.Value);
            }
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task UpdateCiphersAsync(Guid userId, IEnumerable<Core.Vault.Entities.Cipher> ciphers)
    {
        if (!ciphers.Any())
        {
            return;
        }
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entities = Mapper.Map<List<Cipher>>(ciphers);
            await dbContext.BulkCopyAsync(base.DefaultBulkCopyOptions, entities);
            await dbContext.UserBumpAccountRevisionDateAsync(userId);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task UpdatePartialAsync(Guid id, Guid userId, Guid? folderId, bool favorite)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var cipher = await dbContext.Ciphers.FindAsync(id);

            var foldersJson = NSL.JObject.Parse(cipher.Folders);
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

            var favoritesJson = NSL.JObject.Parse(cipher.Favorites);
            if (favorite)
            {
                favoritesJson.Add(userId.ToString(), favorite);
            }
            else
            {
                favoritesJson.Remove(userId.ToString());
            }

            await dbContext.UserBumpAccountRevisionDateAsync(userId);
            await dbContext.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(
        Guid userId, IEnumerable<Core.Vault.Entities.Cipher> ciphers)
    {
        return async (SqlConnection _, SqlTransaction _) =>
        {
            var newCiphers = ciphers.ToList();
            using var scope = ServiceScopeFactory.CreateScope();
            var dbContext = GetDatabaseContext(scope);
            var userCiphers = await GetDbSet(dbContext)
                .Where(c => c.UserId == userId)
                .ToListAsync();
            var validCiphers = userCiphers
                .Where(cipher => newCiphers.Any(newCipher => newCipher.Id == cipher.Id));
            foreach (var cipher in validCiphers)
            {
                var updateCipher = newCiphers.First(newCipher => newCipher.Id == cipher.Id);
                cipher.Data = updateCipher.Data;
                cipher.Attachments = updateCipher.Attachments;
                cipher.RevisionDate = updateCipher.RevisionDate;
                cipher.Key = updateCipher.Key;
            }

            await dbContext.SaveChangesAsync();
        };
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

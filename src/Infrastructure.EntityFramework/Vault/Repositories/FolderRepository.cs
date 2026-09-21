// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using AutoMapper;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories;

public class FolderRepository : Repository<Core.Vault.Entities.Folder, Folder, Guid>, IFolderRepository
{
    public FolderRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Folders)
    { }

    public async Task<Core.Vault.Entities.Folder> GetByIdAsync(Guid id, Guid userId)
    {
        var folder = await base.GetByIdAsync(id);
        if (folder == null || folder.UserId != userId)
        {
            return null;
        }

        return folder;
    }

    public async Task<ICollection<Core.Vault.Entities.Folder>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from f in dbContext.Folders
                        where f.UserId == userId
                        select f;
            var folders = await query.ToListAsync();
            return Mapper.Map<List<Core.Vault.Entities.Folder>>(folders);
        }
    }

    public override async Task DeleteAsync(Core.Vault.Entities.Folder folder)
    {
        await DeleteManyAsync([folder.Id], folder.UserId);
    }

    /// <inheritdoc />
    public async Task DeleteManyAsync(IEnumerable<Guid> folderIds, Guid userId)
    {
        var requestedIds = folderIds.ToList();
        if (requestedIds.Count == 0)
        {
            return;
        }

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var deletingFolders = await GetDbSet(dbContext)
            .Where(f => f.UserId == userId && requestedIds.Contains(f.Id))
            .ToListAsync();

        if (deletingFolders.Count == 0)
        {
            return;
        }

        var deletingFolderIds = deletingFolders.Select(f => f.Id).ToHashSet();

        var userKey = userId.ToString();
        var userCipherDetails = new UserCipherDetailsQuery(userId).Run(dbContext);
        var filedCiphers = from ucd in userCipherDetails
                           join c in dbContext.Ciphers.Where(c => c.Folders != null && c.Folders.Contains(userKey))
                               on ucd.Id equals c.Id
                           select c;

        await filedCiphers.ForEachAsync(cipher =>
        {
            var folders = ReadFolders(cipher.Folders);
            if (folders == null || !folders.TryGetValue(userId, out var folderId) ||
                !deletingFolderIds.Contains(folderId))
            {
                return;
            }

            folders.Remove(userId);
            cipher.Folders = JsonSerializer.Serialize(folders);
        });

        dbContext.RemoveRange(deletingFolders);
        await dbContext.UserBumpAccountRevisionDateAsync(userId);
        await dbContext.SaveChangesAsync();
    }

    private static Dictionary<Guid, Guid> ReadFolders(string foldersJson)
    {
        try
        {
            return CoreHelpers.LoadClassFromJsonData<Dictionary<Guid, Guid>>(foldersJson);
        }
        catch (JsonException)
        {
            // Some Folders maps are stored in an invalid format, such as '{ "", "<ValidGuid>" }', and are
            // treated as unfiled rather than failing the delete for every other cipher in the batch.
            return null;
        }
    }

    /// <inheritdoc />
    public DatabaseTransactionAction UpdateForKeyRotation(
        Guid userId, IEnumerable<Core.Vault.Entities.Folder> folders)
    {
        return async (connection, transaction) =>
        {
            var newFolders = folders.ToList();
            using var scope = ServiceScopeFactory.CreateScope();
            var dbContext = GetTransactionalDatabaseContext(scope, connection, transaction);
            var userFolders = await GetDbSet(dbContext)
                .Where(f => f.UserId == userId)
                .ToListAsync();
            var validFolders = userFolders
                .Where(folder => newFolders.Any(newFolder => newFolder.Id == folder.Id));
            foreach (var folder in validFolders)
            {
                var updateFolder = newFolders.First(newFolder => newFolder.Id == folder.Id);
                folder.Name = updateFolder.Name;
            }

            await dbContext.SaveChangesAsync();
        };
    }
}

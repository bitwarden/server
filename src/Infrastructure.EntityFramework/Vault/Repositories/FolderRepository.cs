using AutoMapper;
using Bit.Core.Auth.UserFeatures.UserKey;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Microsoft.Data.SqlClient;
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

    // The SQL repository has logic to remove references to the deleted folder. That logic depends on AccessAll access
    // logic, which is being deprecated, so it calls different queries based on the FlexibleCollections feature flag.
    // However, EF doesn't update folder references, so it can just delete the object without reference to the feature flag.
    // This is just here to maintain the same interface.
    public async Task DeleteAsync(Core.Vault.Entities.Folder folder, bool useFlexibleCollections) =>
        await base.DeleteAsync(folder);

    /// <inheritdoc />
    public UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(
        Guid userId, IEnumerable<Core.Vault.Entities.Folder> folders)
    {
        return async (SqlConnection _, SqlTransaction _) =>
        {
            var newFolders = folders.ToList();
            using var scope = ServiceScopeFactory.CreateScope();
            var dbContext = GetDatabaseContext(scope);
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

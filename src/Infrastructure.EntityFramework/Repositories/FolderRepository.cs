using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class FolderRepository : Repository<Core.Entities.Folder, Folder, Guid>, IFolderRepository
{
    public FolderRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Folders)
    { }

    public async Task<Core.Entities.Folder> GetByIdAsync(Guid id, Guid userId)
    {
        var folder = await base.GetByIdAsync(id);
        if (folder == null || folder.UserId != userId)
        {
            return null;
        }

        return folder;
    }


    public async Task<ICollection<Core.Entities.Folder>> GetManyByManyIdsAndUserIdAsync(IEnumerable<Guid> collectionIds, Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from f in dbContext.Folders
                        where collectionIds.Contains(f.Id) & userId == f.UserId
                        select f;
            var data = await query.ToArrayAsync();
            return data;
        }
    }

    public async Task<ICollection<Core.Entities.Folder>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from f in dbContext.Folders
                        where f.UserId == userId
                        select f;
            var folders = await query.ToListAsync();
            return Mapper.Map<List<Core.Entities.Folder>>(folders);
        }
    }

    public async Task<ICollection<Core.Entities.Folder>> Update(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from f in dbContext.Folders
                        where f.UserId == userId
                        select f;
            var folders = await query.ToListAsync();
            return Mapper.Map<List<Core.Entities.Folder>>(folders);
        }
    }

    public Task<ICollection<Core.Entities.Folder>> UpdateManyAsync(Guid userId) => throw new NotImplementedException();
}

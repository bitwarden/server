using AutoMapper;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

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
}

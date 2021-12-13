using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Infrastructure.EntityFramework.Repositories
{
    public class FolderRepository : Repository<TableModel.Folder, Folder, Guid>, IFolderRepository
    {
        public FolderRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Folders)
        { }

        public async Task<TableModel.Folder> GetByIdAsync(Guid id, Guid userId)
        {
            var folder = await base.GetByIdAsync(id);
            if (folder == null || folder.UserId != userId)
            {
                return null;
            }

            return folder;
        }

        public async Task<ICollection<TableModel.Folder>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from f in dbContext.Folders
                    where f.UserId == userId
                    select f;
                var folders = await query.ToListAsync();
                return Mapper.Map<List<TableModel.Folder>>(folders);
            }
        }
    }
}

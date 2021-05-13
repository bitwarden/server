using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Table;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class FolderRepository : Repository<TableModel.Folder, EfModel.Folder, Guid>, IFolderRepository
    {
        public FolderRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Folders)
        { }

        public async Task<Folder> GetByIdAsync(Guid id, Guid userId)
        {
            var folder = await base.GetByIdAsync(id);
            if (folder == null || folder.UserId != userId)
            {
                return null;
            }

            return folder;
        }

        public async Task<ICollection<Folder>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from f in dbContext.Folders
                            where f.UserId == userId
                            select f;
                var folders = await query.ToListAsync();
                return (ICollection<Folder>)folders;
            }
        }
    }
}

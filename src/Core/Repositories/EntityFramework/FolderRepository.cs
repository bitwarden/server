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

        public Task<Folder> GetByIdAsync(Guid id, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Folder>> GetManyByUserIdAsync(Guid userId)
        {
            throw new NotImplementedException();
        }
    }
}

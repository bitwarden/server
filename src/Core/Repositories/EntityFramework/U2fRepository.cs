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
    public class U2fRepository : Repository<TableModel.U2f, EfModel.U2f, int>, IU2fRepository
    {
        public U2fRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.U2fs)
        { }

        public async Task<ICollection<U2f>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.U2fs.Where(u => u.UserId == userId).ToListAsync();
                return (ICollection<U2f>)results;
            }
        }

        public async Task DeleteManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var u2fs = dbContext.U2fs.Where(u => u.UserId == userId);
                dbContext.RemoveRange(u2fs);
                await dbContext.SaveChangesAsync();
            }
        }
        
        public override Task ReplaceAsync(U2f obj)
        {
            throw new NotSupportedException();
        }

        public override Task UpsertAsync(U2f obj)
        {
            throw new NotSupportedException();
        }

        public override Task DeleteAsync(U2f obj)
        {
            throw new NotSupportedException();
        }
    }
}

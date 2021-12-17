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
    public class U2fRepository : Repository<TableModel.U2f, U2f, int>, IU2fRepository
    {
        public U2fRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.U2fs)
        { }

        public async Task<ICollection<TableModel.U2f>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.U2fs.Where(u => u.UserId == userId).ToListAsync();
                return (ICollection<TableModel.U2f>)results;
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

        public override Task ReplaceAsync(TableModel.U2f obj)
        {
            throw new NotSupportedException();
        }

        public override Task UpsertAsync(TableModel.U2f obj)
        {
            throw new NotSupportedException();
        }

        public override Task DeleteAsync(TableModel.U2f obj)
        {
            throw new NotSupportedException();
        }
    }
}

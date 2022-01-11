using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories
{
    public class U2fRepository : Repository<Core.Entities.U2f, U2f, int>, IU2fRepository
    {
        public U2fRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.U2fs)
        { }

        public async Task<ICollection<Core.Entities.U2f>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.U2fs.Where(u => u.UserId == userId).ToListAsync();
                return (ICollection<Core.Entities.U2f>)results;
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

        public override Task ReplaceAsync(Core.Entities.U2f obj)
        {
            throw new NotSupportedException();
        }

        public override Task UpsertAsync(Core.Entities.U2f obj)
        {
            throw new NotSupportedException();
        }

        public override Task DeleteAsync(Core.Entities.U2f obj)
        {
            throw new NotSupportedException();
        }
    }
}

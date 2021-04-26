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
    public class SendRepository : Repository<TableModel.Send, EfModel.Send, Guid>, ISendRepository
    {
        public SendRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Sends)
        { }

        public override async Task<Send> CreateAsync(Send send)
        {
           send = await base.CreateAsync(send);

           // User_UpdateStorage
           // User_BumpAccountRevisionDate
           return send;
        }

        public Task<ICollection<Send>> GetManyByDeletionDateAsync(DateTime deletionDateBefore)
        {
           send = await base.CreateAsync(send);

           // User_UpdateStorage
           // User_BumpAccountRevisionDate
           return send;
        }

        public async Task<ICollection<Send>> GetManyByDeletionDateAsync(DateTime deletionDateBefore)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.Sends.Where(s => s.DeletionDate < deletionDateBefore).ToListAsync();
                return Mapper.Map<List<TableModel.Send>>(results);
            }
        }

        public async Task<ICollection<Send>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.Sends.Where(s => s.UserId == userId).ToListAsync();
                return Mapper.Map<List<TableModel.Send>>(results);
            }
        }
    }
}

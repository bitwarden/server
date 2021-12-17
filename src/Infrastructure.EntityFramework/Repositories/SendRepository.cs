﻿using System;
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
    public class SendRepository : Repository<TableModel.Send, Send, Guid>, ISendRepository
    {
        public SendRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Sends)
        { }

        public override async Task<TableModel.Send> CreateAsync(TableModel.Send send)
        {
            send = await base.CreateAsync(send);
            if (send.UserId.HasValue)
            {
                await UserUpdateStorage(send.UserId.Value);
                await UserBumpAccountRevisionDate(send.UserId.Value);
            }
            return send;
        }

        public async Task<ICollection<TableModel.Send>> GetManyByDeletionDateAsync(DateTime deletionDateBefore)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.Sends.Where(s => s.DeletionDate < deletionDateBefore).ToListAsync();
                return Mapper.Map<List<TableModel.Send>>(results);
            }
        }

        public async Task<ICollection<TableModel.Send>> GetManyByUserIdAsync(Guid userId)
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

// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories;

public class CipherHistoryRepository : Repository<Core.Vault.Entities.CipherHistory, CipherHistory, Guid>, ICipherHistoryRepository
{
    public CipherHistoryRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.CipherHistories)
    { }

    public async Task<ICollection<Core.Vault.Entities.CipherHistory>> GetManyByCipherIdAsync(Guid cipherId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entities = await GetDbSet(dbContext)
                .Where(history => history.CipherId == cipherId)
                .OrderByDescending(history => history.HistoryDate)
                .ToListAsync();

            return Mapper.Map<ICollection<Core.Vault.Entities.CipherHistory>>(entities);
        }
    }
}

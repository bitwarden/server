using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class PlayItemRepository : Repository<Core.Entities.PlayItem, PlayItem, Guid>, IPlayItemRepository
{
    public PlayItemRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.PlayItem)
    { }

    public async Task<ICollection<Core.Entities.PlayItem>> GetByPlayIdAsync(string playId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var playItemEntities = await GetDbSet(dbContext)
                .Where(pd => pd.PlayId == playId)
                .ToListAsync();
            return Mapper.Map<List<Core.Entities.PlayItem>>(playItemEntities);
        }
    }

    public async Task DeleteByPlayIdAsync(string playId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entities = await GetDbSet(dbContext)
                .Where(pd => pd.PlayId == playId)
                .ToListAsync();

            dbContext.PlayItem.RemoveRange(entities);
            await dbContext.SaveChangesAsync();
        }
    }
}

using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class PlayDataRepository : Repository<Core.Entities.PlayData, PlayData, Guid>, IPlayDataRepository
{
    public PlayDataRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.PlayData)
    { }

    public async Task<ICollection<Core.Entities.PlayData>> GetByPlayIdAsync(string playId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var playDataEntities = await GetDbSet(dbContext)
                .Where(pd => pd.PlayId == playId)
                .ToListAsync();
            return Mapper.Map<List<Core.Entities.PlayData>>(playDataEntities);
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

            dbContext.PlayData.RemoveRange(entities);
            await dbContext.SaveChangesAsync();
        }
    }
}

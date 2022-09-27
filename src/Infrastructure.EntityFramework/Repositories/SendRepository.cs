using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class SendRepository : Repository<Core.Entities.Send, Send, Guid>, ISendRepository
{
    public SendRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Sends)
    { }

    public override async Task<Core.Entities.Send> CreateAsync(Core.Entities.Send send)
    {
        send = await base.CreateAsync(send);
        if (send.UserId.HasValue)
        {
            await UserUpdateStorage(send.UserId.Value);
            await UserBumpAccountRevisionDate(send.UserId.Value);
        }
        return send;
    }

    public async Task<ICollection<Core.Entities.Send>> GetManyByDeletionDateAsync(DateTime deletionDateBefore)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Sends.Where(s => s.DeletionDate < deletionDateBefore).ToListAsync();
            return Mapper.Map<List<Core.Entities.Send>>(results);
        }
    }

    public async Task<ICollection<Core.Entities.Send>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Sends.Where(s => s.UserId == userId).ToListAsync();
            return Mapper.Map<List<Core.Entities.Send>>(results);
        }
    }
}

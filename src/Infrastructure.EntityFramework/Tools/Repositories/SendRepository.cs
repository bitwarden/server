using AutoMapper;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Tools.Repositories;

public class SendRepository : Repository<Core.Tools.Entities.Send, Send, Guid>, ISendRepository
{
    public SendRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Sends)
    { }

    public override async Task<Core.Tools.Entities.Send> CreateAsync(Core.Tools.Entities.Send send)
    {
        send = await base.CreateAsync(send);
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            if (send.UserId.HasValue)
            {
                await UserUpdateStorage(send.UserId.Value);
                await dbContext.UserBumpAccountRevisionDateAsync(send.UserId.Value);
                await dbContext.SaveChangesAsync();
            }
        }

        return send;
    }

    public async Task<ICollection<Core.Tools.Entities.Send>> GetManyByDeletionDateAsync(DateTime deletionDateBefore)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Sends.Where(s => s.DeletionDate < deletionDateBefore).ToListAsync();
            return Mapper.Map<List<Core.Tools.Entities.Send>>(results);
        }
    }

    public async Task<ICollection<Core.Tools.Entities.Send>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.Sends.Where(s => s.UserId == userId).ToListAsync();
            return Mapper.Map<List<Core.Tools.Entities.Send>>(results);
        }
    }
}

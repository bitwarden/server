using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class AuthRequestRepository : Repository<Core.Entities.AuthRequest, AuthRequest, Guid>, IAuthRequestRepository
{
    public AuthRequestRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.AuthRequests)
    { }
    public async Task<int> DeleteExpiredAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var expiredRequests = await dbContext.AuthRequests.Where(a => a.CreationDate < DateTime.Now.AddMinutes(-15)).ToListAsync();
            dbContext.AuthRequests.RemoveRange(expiredRequests);
            await dbContext.SaveChangesAsync();
            return 1;
        }
    }

    public async Task<ICollection<Core.Entities.AuthRequest>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var userAuthRequests = await dbContext.AuthRequests.Where(a => a.UserId.Equals(userId)).ToListAsync();
            return Mapper.Map<List<Core.Entities.AuthRequest>>(userAuthRequests);
        }
    }
}

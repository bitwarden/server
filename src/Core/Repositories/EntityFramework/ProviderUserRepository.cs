using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Repositories.EntityFramework;
using TableModel = Bit.Core.Models.Table;
using EfModel = Bit.Core.Models.EntityFramework;
using Microsoft.Extensions.DependencyInjection;
using AutoMapper;
using Bit.Core.Enums.Provider;
using Microsoft.EntityFrameworkCore;

namespace Bit.Core.Repositories
{
    public class ProviderUserRepository : 
        Repository<TableModel.Provider.ProviderUser, EfModel.Provider.ProviderUser, Guid>, IProviderUserRepository
    {
        public ProviderUserRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.ProviderUsers)
        { }

        public async Task<int> GetCountByProviderAsync(Guid providerId, string email, bool onlyRegisteredUsers)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = from pu in dbContext.ProviderUsers
                    join u in dbContext.Users
                        on pu.UserId equals u.Id into u_g
                    from u in u_g.DefaultIfEmpty()
                    where pu.ProviderId == providerId &&
                        ((!onlyRegisteredUsers && (pu.Email == email || u.Email == email)) ||
                        (onlyRegisteredUsers && u.Email == email))
                    select new { pu, u };
                return await query.CountAsync();
            }
        }

        public async Task<ICollection<ProviderUser>> GetManyAsync(IEnumerable<Guid> ids)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = dbContext.ProviderUsers.Where(item => ids.Contains(item.Id));
                return await query.ToArrayAsync();
            }
        }

        public async Task<ICollection<ProviderUser>> GetManyByProviderAsync(Guid providerId, ProviderUserType? type = null)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = dbContext.ProviderUsers.Where(pu => pu.ProviderId.Equals(providerId) && 
                    (type != null && pu.Type.Equals(type)));
                return await query.ToArrayAsync();
            }
        }

        public async Task DeleteManyAsync(IEnumerable<Guid> providerUserIds)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                // TODO: User_BumpAccountRevisionDatebyProviderUserIds
                var entities = dbContext.ProviderUsers.Where(pu => providerUserIds.Contains(pu.Id));
                dbContext.ProviderUsers.RemoveRange(entities);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}

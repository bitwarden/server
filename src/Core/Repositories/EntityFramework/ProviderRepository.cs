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
using Microsoft.EntityFrameworkCore;

namespace Bit.Core.Repositories.EntityFramework
{
    public class ProviderRepository : Repository<TableModel.Provider.Provider, EfModel.Provider.Provider, Guid>, IProviderRepository
    {

        public ProviderRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Providers)
        { }

        public async Task<ICollection<Provider>> SearchAsync(string name, string userEmail, int skip, int take)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = !string.IsNullOrWhiteSpace(userEmail) ?
                    (from p in dbContext.Providers
                    join pu in dbContext.ProviderUsers
                        on p.Id equals pu.ProviderId
                    join u in dbContext.Users
                        on pu.UserId equals u.Id
                    where (string.IsNullOrWhiteSpace(name) || p.Name.Contains(name)) &&
                        u.Email == userEmail
                    orderby p.CreationDate descending
                    select new { p, pu, u }).Skip(skip).Take(take).Select(x => x.p) :
                    (from p in dbContext.Providers
                    where string.IsNullOrWhiteSpace(name) || p.Name.Contains(name)
                    orderby p.CreationDate descending
                    select new { p }).Skip(skip).Take(take).Select(x => x.p);
                return await query.ToArrayAsync();
            }
        }
    }
}

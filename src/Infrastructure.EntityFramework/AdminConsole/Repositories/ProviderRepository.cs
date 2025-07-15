﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class ProviderRepository : Repository<Provider, Models.Provider.Provider, Guid>, IProviderRepository
{

    public ProviderRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.Providers)
    { }

    public override async Task DeleteAsync(Provider provider)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.UserBumpAccountRevisionDateByProviderIdAsync(provider.Id);
            await dbContext.SaveChangesAsync();
        }
        await base.DeleteAsync(provider);
    }

    public async Task<Provider> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from p in dbContext.Providers
                        join po in dbContext.ProviderOrganizations
                            on p.Id equals po.ProviderId
                        where po.OrganizationId == organizationId
                        select p;
            return await query.FirstOrDefaultAsync();
        }
    }

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
            var providers = await query.ToArrayAsync();
            return Mapper.Map<List<Provider>>(providers);
        }
    }

    public async Task<ICollection<ProviderAbility>> GetManyAbilitiesAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return await GetDbSet(dbContext)
                .Select(e => new ProviderAbility
                {
                    Enabled = e.Enabled,
                    Id = e.Id,
                    UseEvents = e.UseEvents,
                }).ToListAsync();
        }
    }
}

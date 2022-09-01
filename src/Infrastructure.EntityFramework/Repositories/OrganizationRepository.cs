using AutoMapper;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class OrganizationRepository : Repository<Core.Entities.Organization, Organization, Guid>, IOrganizationRepository
{
    public OrganizationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Organizations)
    { }

    public async Task<Core.Entities.Organization> GetByIdentifierAsync(string identifier)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organization = await GetDbSet(dbContext).Where(e => e.Identifier == identifier)
                .FirstOrDefaultAsync();
            return organization;
        }
    }

    public async Task<ICollection<Core.Entities.Organization>> GetManyByEnabledAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organizations = await GetDbSet(dbContext).Where(e => e.Enabled).ToListAsync();
            return Mapper.Map<List<Core.Entities.Organization>>(organizations);
        }
    }

    public async Task<ICollection<Core.Entities.Organization>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organizations = await GetDbSet(dbContext)
                .Select(e => e.OrganizationUsers
                    .Where(ou => ou.UserId == userId)
                    .Select(ou => ou.Organization))
                .ToListAsync();
            return Mapper.Map<List<Core.Entities.Organization>>(organizations);
        }
    }

    public async Task<ICollection<Core.Entities.Organization>> SearchAsync(string name, string userEmail,
        bool? paid, int skip, int take)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var organizations = await GetDbSet(dbContext)
                .Where(e => name == null || e.Name.Contains(name))
                .Where(e => userEmail == null || e.OrganizationUsers.Any(u => u.Email == userEmail))
                .Where(e => paid == null ||
                        (paid == true && !string.IsNullOrWhiteSpace(e.GatewaySubscriptionId)) ||
                        (paid == false && e.GatewaySubscriptionId == null))
                .OrderBy(e => e.CreationDate)
                .Skip(skip).Take(take)
                .ToListAsync();
            return Mapper.Map<List<Core.Entities.Organization>>(organizations);
        }
    }

    public async Task<ICollection<OrganizationAbility>> GetManyAbilitiesAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return await GetDbSet(dbContext)
            .Select(e => new OrganizationAbility
            {
                Enabled = e.Enabled,
                Id = e.Id,
                Use2fa = e.Use2fa,
                UseEvents = e.UseEvents,
                UsersGetPremium = e.UsersGetPremium,
                Using2fa = e.Use2fa && e.TwoFactorProviders != null,
                UseSso = e.UseSso,
                UseKeyConnector = e.UseKeyConnector,
                UseResetPassword = e.UseResetPassword,
                UseScim = e.UseScim,
            }).ToListAsync();
        }
    }

    public async Task UpdateStorageAsync(Guid id)
    {
        await OrganizationUpdateStorage(id);
    }

    public override async Task DeleteAsync(Core.Entities.Organization organization)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgEntity = await dbContext.FindAsync<Organization>(organization.Id);

            dbContext.Remove(orgEntity);
            await dbContext.SaveChangesAsync();
        }
    }
}

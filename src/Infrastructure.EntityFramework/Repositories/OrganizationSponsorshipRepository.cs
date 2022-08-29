using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class OrganizationSponsorshipRepository : Repository<Core.Entities.OrganizationSponsorship, OrganizationSponsorship, Guid>, IOrganizationSponsorshipRepository
{
    public OrganizationSponsorshipRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationSponsorships)
    { }

    public async Task<ICollection<Guid>> CreateManyAsync(IEnumerable<Core.Entities.OrganizationSponsorship> organizationSponsorships)
    {
        if (!organizationSponsorships.Any())
        {
            return new List<Guid>();
        }

        foreach (var organizationSponsorship in organizationSponsorships)
        {
            organizationSponsorship.SetNewId();
        }

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entities = Mapper.Map<List<OrganizationUser>>(organizationSponsorships);
            await dbContext.AddRangeAsync(entities);
            await dbContext.SaveChangesAsync();
        }

        return organizationSponsorships.Select(u => u.Id).ToList();
    }

    public async Task ReplaceManyAsync(IEnumerable<Core.Entities.OrganizationSponsorship> organizationSponsorships)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            dbContext.UpdateRange(organizationSponsorships);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task UpsertManyAsync(IEnumerable<Core.Entities.OrganizationSponsorship> organizationSponsorships)
    {
        var createSponsorships = new List<Core.Entities.OrganizationSponsorship>();
        var replaceSponsorships = new List<Core.Entities.OrganizationSponsorship>();
        foreach (var organizationSponsorship in organizationSponsorships)
        {
            if (organizationSponsorship.Id.Equals(default))
            {
                createSponsorships.Add(organizationSponsorship);
            }
            else
            {
                replaceSponsorships.Add(organizationSponsorship);
            }
        }

        await CreateManyAsync(createSponsorships);
        await ReplaceManyAsync(replaceSponsorships);
    }

    public async Task DeleteManyAsync(IEnumerable<Guid> organizationSponsorshipIds)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entities = await dbContext.OrganizationSponsorships
                .Where(os => organizationSponsorshipIds.Contains(os.Id))
                .ToListAsync();

            dbContext.OrganizationSponsorships.RemoveRange(entities);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<Core.Entities.OrganizationSponsorship> GetByOfferedToEmailAsync(string email)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgSponsorship = await GetDbSet(dbContext).Where(e => e.OfferedToEmail == email)
                .FirstOrDefaultAsync();
            return orgSponsorship;
        }
    }

    public async Task<Core.Entities.OrganizationSponsorship> GetBySponsoredOrganizationIdAsync(Guid sponsoredOrganizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgSponsorship = await GetDbSet(dbContext).Where(e => e.SponsoredOrganizationId == sponsoredOrganizationId)
                .FirstOrDefaultAsync();
            return orgSponsorship;
        }
    }

    public async Task<Core.Entities.OrganizationSponsorship> GetBySponsoringOrganizationUserIdAsync(Guid sponsoringOrganizationUserId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgSponsorship = await GetDbSet(dbContext).Where(e => e.SponsoringOrganizationUserId == sponsoringOrganizationUserId)
                .FirstOrDefaultAsync();
            return orgSponsorship;
        }
    }

    public async Task<DateTime?> GetLatestSyncDateBySponsoringOrganizationIdAsync(Guid sponsoringOrganizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return await GetDbSet(dbContext).Where(e => e.SponsoringOrganizationId == sponsoringOrganizationId && e.LastSyncDate != null)
                .OrderByDescending(e => e.LastSyncDate)
                .Select(e => e.LastSyncDate)
                .FirstOrDefaultAsync();

        }
    }

    public async Task<ICollection<Core.Entities.OrganizationSponsorship>> GetManyBySponsoringOrganizationAsync(Guid sponsoringOrganizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from os in dbContext.OrganizationSponsorships
                        where os.SponsoringOrganizationId == sponsoringOrganizationId
                        select os;
            return Mapper.Map<List<Core.Entities.OrganizationSponsorship>>(await query.ToListAsync());
        }
    }

}

using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class OrganizationDomainRepository : Repository<Core.Entities.OrganizationDomain, OrganizationDomain, Guid>, IOrganizationDomainRepository
{
    public OrganizationDomainRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationDomains)
    {
    }

    public async Task<ICollection<Core.Entities.OrganizationDomain>> GetClaimedDomainsByDomainNameAsync(
        string domainName)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var claimedDomains = await dbContext.OrganizationDomains
            .Where(x => x.DomainName == domainName
                        && x.VerifiedDate != null)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<Core.Entities.OrganizationDomain>>(claimedDomains);
    }

    public async Task<ICollection<Core.Entities.OrganizationDomain>> GetDomainsByOrganizationId(Guid orgId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var domains = await dbContext.OrganizationDomains
            .Where(x => x.OrganizationId == orgId)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<Core.Entities.OrganizationDomain>>(domains);
    }

    public async Task<ICollection<Core.Entities.OrganizationDomain>> GetManyByNextRunDateAsync(DateTime date)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        List<OrganizationDomain> pastDomains;

        var domains = await dbContext.OrganizationDomains
            .Where(x => x.VerifiedDate == null
                        && x.JobRunCount != 3
                        && x.NextRunDate.Year == date.Year
                        && x.NextRunDate.Month == date.Month
                        && x.NextRunDate.Day == date.Day
                        && x.NextRunDate.Hour == date.Hour)
            .AsNoTracking()
            .ToListAsync();

        //Get records that have ignored/failed by the background service
        if (dbContext.Database.IsNpgsql())
        {
            pastDomains = dbContext.OrganizationDomains
                .AsEnumerable()
                .Where(x => (date - x.NextRunDate).TotalHours > 36
                && x.VerifiedDate == null
                && x.JobRunCount != 3)
                .ToList();
        }
        else
        {
            pastDomains = await dbContext.OrganizationDomains
                .Where(x => EF.Functions.DateDiffHour(x.NextRunDate, date) > 36
                    && x.VerifiedDate == null
                    && x.JobRunCount != 3)
                .ToListAsync();
        }

        var results = domains.Union(pastDomains);

        return Mapper.Map<List<Core.Entities.OrganizationDomain>>(results);
    }
}

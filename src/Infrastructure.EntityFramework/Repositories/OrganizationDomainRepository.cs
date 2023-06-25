using System.Net.Mail;
using AutoMapper;
using Bit.Core.Models.Data.Organizations;
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

    public async Task<ICollection<Core.Entities.OrganizationDomain>> GetDomainsByOrganizationIdAsync(Guid orgId)
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
        var pastDomains = dbContext.OrganizationDomains
            .AsEnumerable()
            .Where(x => (date - x.NextRunDate).TotalHours > 36
                        && x.VerifiedDate == null
                        && x.JobRunCount != 3)
            .ToList();

        var results = domains.Union(pastDomains);

        return Mapper.Map<List<Core.Entities.OrganizationDomain>>(results);
    }

    public async Task<OrganizationDomainSsoDetailsData> GetOrganizationDomainSsoDetailsAsync(string email)
    {
        var domainName = new MailAddress(email).Host;

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var ssoDetails = await (from o in dbContext.Organizations
                                from od in o.Domains
                                join s in dbContext.SsoConfigs on o.Id equals s.OrganizationId into sJoin
                                from s in sJoin.DefaultIfEmpty()
                                where od.DomainName == domainName && o.Enabled
                                select new OrganizationDomainSsoDetailsData
                                {
                                    OrganizationId = o.Id,
                                    OrganizationName = o.Name,
                                    SsoAvailable = o.SsoConfigs.Any(sc => sc.Enabled),
                                    OrganizationIdentifier = o.Identifier,
                                    VerifiedDate = od.VerifiedDate,
                                    DomainName = od.DomainName
                                })
            .AsNoTracking()
            .SingleOrDefaultAsync();

        return ssoDetails;
    }

    public async Task<Core.Entities.OrganizationDomain> GetDomainByOrgIdAndDomainNameAsync(Guid orgId, string domainName)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var domain = await dbContext.OrganizationDomains
            .Where(x => x.OrganizationId == orgId && x.DomainName == domainName)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return Mapper.Map<Core.Entities.OrganizationDomain>(domain);
    }

    public async Task<ICollection<Core.Entities.OrganizationDomain>> GetExpiredOrganizationDomainsAsync()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        //Get domains that have not been verified after 72 hours
        var domains = await dbContext.OrganizationDomains
            .Where(x => (DateTime.UtcNow - x.CreationDate).Days == 4
                        && x.VerifiedDate == null)
            .AsNoTracking()
            .ToListAsync();

        return Mapper.Map<List<Core.Entities.OrganizationDomain>>(domains);
    }

    public async Task<bool> DeleteExpiredAsync(int expirationPeriod)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var expiredDomains = await dbContext.OrganizationDomains
            .Where(x => x.LastCheckedDate < DateTime.UtcNow.AddDays(-expirationPeriod))
            .ToListAsync();
        dbContext.OrganizationDomains.RemoveRange(expiredDomains);
        return await dbContext.SaveChangesAsync() > 0;
    }
}

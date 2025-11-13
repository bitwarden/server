using System.Net.Mail;
using AutoMapper;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

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

        var start36HoursWindow = date.AddHours(-36);
        var end36HoursWindow = date;

        var pastDomains = await dbContext.OrganizationDomains
            .Where(x => x.NextRunDate >= start36HoursWindow
                       && x.NextRunDate <= end36HoursWindow
                       && x.VerifiedDate == null
                       && x.JobRunCount != 3)
            .ToListAsync();

        return Mapper.Map<List<Core.Entities.OrganizationDomain>>(pastDomains);
    }

    public async Task<OrganizationDomainSsoDetailsData?> GetOrganizationDomainSsoDetailsAsync(string email)
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

    public async Task<IEnumerable<VerifiedOrganizationDomainSsoDetail>> GetVerifiedOrganizationDomainSsoDetailsAsync(string email)
    {
        var domainName = new MailAddress(email).Host;

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await (from o in dbContext.Organizations
                      from od in o.Domains
                      join s in dbContext.SsoConfigs on o.Id equals s.OrganizationId into sJoin
                      from s in sJoin.DefaultIfEmpty()
                      where od.DomainName == domainName
                            && o.Enabled
                            && s.Enabled
                            && od.VerifiedDate != null
                      select new VerifiedOrganizationDomainSsoDetail(
                          o.Id,
                          o.Name,
                          od.DomainName,
                          o.Identifier))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Core.Entities.OrganizationDomain?> GetDomainByIdOrganizationIdAsync(Guid id, Guid orgId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var domain = await dbContext.OrganizationDomains
            .Where(x => x.Id == id && x.OrganizationId == orgId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return Mapper.Map<Core.Entities.OrganizationDomain>(domain);
    }

    public async Task<Core.Entities.OrganizationDomain?> GetDomainByOrgIdAndDomainNameAsync(Guid orgId, string domainName)
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

        var threeDaysOldUnverifiedDomains = await dbContext.OrganizationDomains
            .Where(x => x.CreationDate.Date == DateTime.UtcNow.AddDays(-4).Date
                      && x.VerifiedDate == null)
            .AsNoTracking()
            .ToListAsync();

        return Mapper.Map<List<Core.Entities.OrganizationDomain>>(threeDaysOldUnverifiedDomains);
    }

    public async Task<bool> DeleteExpiredAsync(int expirationPeriod)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var expiredDomains = await dbContext.OrganizationDomains
            .Where(x => x.LastCheckedDate < DateTime.UtcNow.AddDays(-expirationPeriod) && x.VerifiedDate == null)
            .ToListAsync();
        dbContext.OrganizationDomains.RemoveRange(expiredDomains);
        return await dbContext.SaveChangesAsync() > 0;
    }

    public async Task<IEnumerable<Core.Entities.OrganizationDomain>> GetVerifiedDomainsByOrganizationIdsAsync(
        IEnumerable<Guid> organizationIds)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var verifiedDomains = await (from d in dbContext.OrganizationDomains
                                     where organizationIds.Contains(d.OrganizationId) && d.VerifiedDate != null
                                     select new OrganizationDomain
                                     {
                                         OrganizationId = d.OrganizationId,
                                         DomainName = d.DomainName
                                     })
            .AsNoTracking()
            .ToListAsync();

        return Mapper.Map<List<OrganizationDomain>>(verifiedDomains);
    }

    public async Task<bool> HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(string domainName, Guid? excludeOrganizationId = null)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var query = from od in dbContext.OrganizationDomains
                    join o in dbContext.Organizations on od.OrganizationId equals o.Id
                    join p in dbContext.Policies on o.Id equals p.OrganizationId
                    where od.DomainName == domainName
                        && od.VerifiedDate != null
                        && o.Enabled
                        && o.UsePolicies
                        && o.UseOrganizationDomains
                        && (!excludeOrganizationId.HasValue || o.Id != excludeOrganizationId.Value)
                        && p.Type == Core.AdminConsole.Enums.PolicyType.BlockClaimedDomainAccountCreation
                        && p.Enabled
                    select od;

        return await query.AnyAsync();
    }
}


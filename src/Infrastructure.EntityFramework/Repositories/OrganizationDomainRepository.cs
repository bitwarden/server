using System.Net.Mail;
using AutoMapper;
using Bit.Core.Enums;
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

    public async Task<OrganizationDomainSsoDetailsData> GetOrganizationDomainSsoDetailsAsync(string email)
    {
        var domainName = new MailAddress(email).Host;

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var ssoDetails = await dbContext.Organizations
            .Join(dbContext.OrganizationDomains, o => o.Id, od => od.OrganizationId,
                (organization, domain) => new { resOrganization = organization, resDomain = domain })
            .Join(dbContext.Policies, o => o.resOrganization.Id, p => p.OrganizationId,
                (combinedOrgDomain, policy)
                    => new
                    {
                        Organization = combinedOrgDomain.resOrganization,
                        Domain = combinedOrgDomain.resDomain,
                        Policy = policy
                    })
            .Select(x => new OrganizationDomainSsoDetailsData
            {
                OrganizationId = x.Organization.Id,
                OrganizationName = x.Organization.Name,
                SsoAvailable = x.Organization.UseSso,
                OrganizationIdentifier = x.Organization.Identifier,
                SsoRequired = x.Policy.Enabled,
                VerifiedDate = x.Domain.VerifiedDate,
                PolicyType = x.Policy.Type,
                DomainName = x.Domain.DomainName,
                OrganizationEnabled = x.Organization.Enabled
            })
            .Where(y => y.DomainName == domainName
                        && y.OrganizationEnabled == true
                        && y.PolicyType.Equals(PolicyType.RequireSso))
            .AsNoTracking()
            .SingleOrDefaultAsync();

        return ssoDetails;
    }

    public async Task<Core.Entities.OrganizationDomain> GetDomainByOrganizationIdAsync(Guid orgId, string domainName)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var domain = await dbContext.OrganizationDomains
            .Where(x => x.OrganizationId == orgId && x.DomainName == domainName)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return Mapper.Map<Core.Entities.OrganizationDomain>(domain);
    }
}

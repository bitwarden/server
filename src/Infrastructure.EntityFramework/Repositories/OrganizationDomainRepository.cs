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

    public async Task<OrganizationDomainSsoDetailsData> GetOrganizationDomainSsoDetails(string email)
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
}

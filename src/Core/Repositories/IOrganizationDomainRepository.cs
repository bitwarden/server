using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Repositories;

public interface IOrganizationDomainRepository : IRepository<OrganizationDomain, Guid>
{
    Task<ICollection<OrganizationDomain>> GetClaimedDomainsByDomainNameAsync(string domainName);
    Task<ICollection<OrganizationDomain>> GetDomainsByOrganizationIdAsync(Guid orgId);
    Task<ICollection<OrganizationDomain>> GetManyByNextRunDateAsync(DateTime date);
    Task<OrganizationDomainSsoDetailsData> GetOrganizationDomainSsoDetailsAsync(string email);
    Task<OrganizationDomain> GetDomainByOrgIdAndDomainNameAsync(Guid orgId, string domainName);
    Task<ICollection<OrganizationDomain>> GetExpiredOrganizationDomainsAsync();
    Task<bool> DeleteExpiredAsync();
}

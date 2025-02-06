using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;

#nullable enable

namespace Bit.Core.Repositories;

public interface IOrganizationDomainRepository : IRepository<OrganizationDomain, Guid>
{
    Task<ICollection<OrganizationDomain>> GetClaimedDomainsByDomainNameAsync(string domainName);
    Task<ICollection<OrganizationDomain>> GetDomainsByOrganizationIdAsync(Guid orgId);
    Task<ICollection<OrganizationDomain>> GetManyByNextRunDateAsync(DateTime date);
    Task<OrganizationDomainSsoDetailsData?> GetOrganizationDomainSsoDetailsAsync(string email);
    Task<IEnumerable<VerifiedOrganizationDomainSsoDetail>> GetVerifiedOrganizationDomainSsoDetailsAsync(string email);
    Task<OrganizationDomain?> GetDomainByIdOrganizationIdAsync(Guid id, Guid organizationId);
    Task<OrganizationDomain?> GetDomainByOrgIdAndDomainNameAsync(Guid orgId, string domainName);
    Task<ICollection<OrganizationDomain>> GetExpiredOrganizationDomainsAsync();
    Task<bool> DeleteExpiredAsync(int expirationPeriod);
}

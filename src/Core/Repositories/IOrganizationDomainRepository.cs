using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Repositories;

public interface IOrganizationDomainRepository : IRepository<OrganizationDomain, Guid>
{
    Task<ICollection<OrganizationDomain>> GetClaimedDomainsByDomainNameAsync(string domainName);
    Task<ICollection<OrganizationDomain>> GetDomainsByOrganizationId(Guid orgId);
    Task<OrganizationDomainSsoDetailsData> GetOrganizationDomainSsoDetails(string email);
}

using Bit.Core.Entities;

namespace Bit.Core.Repositories;

public interface IOrganizationDomainRepository : IRepository<OrganizationDomain, Guid>
{
    Task<ICollection<OrganizationDomain>> GetClaimedDomainsByDomainNameAsync(string domainName);
    Task<ICollection<OrganizationDomain>> GetDomainsByOrganizationId(Guid orgId);
    Task<ICollection<OrganizationDomain>> GetManyByNextRunDateAsync(DateTime date);
}

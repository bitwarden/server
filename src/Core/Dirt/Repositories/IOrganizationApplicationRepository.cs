using Bit.Core.Dirt.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Repositories;

public interface IOrganizationApplicationRepository : IRepository<OrganizationApplication, Guid>
{
    Task<ICollection<OrganizationApplication>> GetByOrganizationIdAsync(Guid organizationId);
}

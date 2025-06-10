using Bit.Core.Dirt.Reports.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Reports.Repositories;

public interface IOrganizationApplicationRepository : IRepository<OrganizationApplication, Guid>
{
    Task<ICollection<OrganizationApplication>> GetByOrganizationIdAsync(Guid organizationId);
}

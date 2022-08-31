using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Repositories;

public interface IOrganizationConnectionRepository : IRepository<OrganizationConnection, Guid>
{
    Task<ICollection<OrganizationConnection>> GetByOrganizationIdTypeAsync(Guid organizationId, OrganizationConnectionType type);
    Task<ICollection<OrganizationConnection>> GetEnabledByOrganizationIdTypeAsync(Guid organizationId, OrganizationConnectionType type);
}

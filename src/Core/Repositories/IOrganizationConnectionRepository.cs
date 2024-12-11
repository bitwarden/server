using Bit.Core.Entities;
using Bit.Core.Enums;

#nullable enable

namespace Bit.Core.Repositories;

public interface IOrganizationConnectionRepository : IRepository<OrganizationConnection, Guid>
{
    Task<OrganizationConnection?> GetByIdOrganizationIdAsync(Guid id, Guid organizationId);
    Task<ICollection<OrganizationConnection>> GetByOrganizationIdTypeAsync(
        Guid organizationId,
        OrganizationConnectionType type
    );
    Task<ICollection<OrganizationConnection>> GetEnabledByOrganizationIdTypeAsync(
        Guid organizationId,
        OrganizationConnectionType type
    );
}

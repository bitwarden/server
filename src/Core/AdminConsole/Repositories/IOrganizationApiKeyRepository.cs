using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.Repositories;

public interface IOrganizationApiKeyRepository : IRepository<OrganizationApiKey, Guid>
{
    Task<IEnumerable<OrganizationApiKey>> GetManyByOrganizationIdTypeAsync(Guid organizationId, OrganizationApiKeyType? type = null);
}

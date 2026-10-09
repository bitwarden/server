using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.Repositories;

public interface IOrganizationScopedApiKeyRepository : IRepository<OrganizationScopedApiKey, Guid>
{
    Task<ICollection<OrganizationScopedApiKey>> GetManyByOrganizationIdAsync(Guid organizationId);
}

using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys.Interfaces;

public interface IGetOrganizationScopedApiKeysQuery
{
    Task<ICollection<OrganizationScopedApiKey>> GetManyByOrganizationIdAsync(Guid organizationId);
}

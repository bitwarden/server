using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys.Interfaces;

public interface IGetScopedOrganizationApiKeysQuery
{
    Task<ICollection<ApiKey>> GetManyByOrganizationIdAsync(Guid organizationId);
}

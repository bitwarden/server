using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys.Interfaces;

public interface IGetOrganizationApiKeyQuery
{
    Task<OrganizationApiKey> GetOrganizationApiKeyAsync(Guid organizationId, OrganizationApiKeyType organizationApiKeyType);
}

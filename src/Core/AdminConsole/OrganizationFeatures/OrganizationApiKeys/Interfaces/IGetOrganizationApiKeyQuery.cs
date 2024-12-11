using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys.Interfaces;

public interface IGetOrganizationApiKeyQuery
{
    Task<OrganizationApiKey> GetOrganizationApiKeyAsync(
        Guid organizationId,
        OrganizationApiKeyType organizationApiKeyType
    );
}

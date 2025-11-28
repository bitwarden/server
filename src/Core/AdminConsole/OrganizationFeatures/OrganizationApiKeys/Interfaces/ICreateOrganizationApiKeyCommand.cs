using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys.Interfaces;

public interface ICreateOrganizationApiKeyCommand
{
    Task<OrganizationApiKey> CreateAsync(Guid organizationId, OrganizationApiKeyType organizationApiKeyType);
}

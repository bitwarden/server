using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys.Interfaces;

public interface ICreateOrganizationApiKeyCommand
{
    Task<OrganizationApiKey> CreateAsync(Guid organizationId, OrganizationApiKeyType organizationApiKeyType);
}

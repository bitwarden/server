using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.OrganizationFeatures.OrganizationApiKeys.Interfaces;

public interface ICreateOrganizationApiKeyCommand
{
    Task<OrganizationApiKey> CreateAsync(Guid organizationId, OrganizationApiKeyType organizationApiKeyType);
}

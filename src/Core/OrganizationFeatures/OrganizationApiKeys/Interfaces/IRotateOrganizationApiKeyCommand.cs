using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationApiKeys.Interfaces;

public interface IRotateOrganizationApiKeyCommand
{
    Task<OrganizationApiKey> RotateApiKeyAsync(OrganizationApiKey organizationApiKey);
}

using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys.Interfaces;

public interface IRotateOrganizationApiKeyCommand
{
    Task<OrganizationApiKey> RotateApiKeyAsync(OrganizationApiKey organizationApiKey);
}

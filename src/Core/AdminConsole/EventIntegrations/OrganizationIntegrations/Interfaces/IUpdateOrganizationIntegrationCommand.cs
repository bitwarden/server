using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations.Interfaces;

public interface IUpdateOrganizationIntegrationCommand
{
    Task<OrganizationIntegration> UpdateAsync(Guid organizationId, Guid integrationId, OrganizationIntegration updatedIntegration);
}

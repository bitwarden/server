using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations.Interfaces;

public interface IUpdateOrganizationIntegrationCommand
{
    Task<OrganizationIntegration> UpdateAsync(Guid organizationId, Guid integrationId, OrganizationIntegration updatedIntegration);
}

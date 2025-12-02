using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations.Interfaces;

public interface ICreateOrganizationIntegrationCommand
{
    Task<OrganizationIntegration> CreateAsync(OrganizationIntegration integration);
}

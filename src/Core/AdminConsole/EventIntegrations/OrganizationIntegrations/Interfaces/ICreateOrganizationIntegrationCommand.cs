using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations.Interfaces;

public interface ICreateOrganizationIntegrationCommand
{
    Task<OrganizationIntegration> CreateAsync(OrganizationIntegration integration);
}

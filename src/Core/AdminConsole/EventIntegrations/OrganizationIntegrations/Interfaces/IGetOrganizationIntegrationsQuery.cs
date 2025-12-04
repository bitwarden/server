using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations.Interfaces;

public interface IGetOrganizationIntegrationsQuery
{
    Task<List<OrganizationIntegration>> GetManyByOrganizationAsync(Guid organizationId);
}

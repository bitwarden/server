namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations.Interfaces;

public interface IDeleteOrganizationIntegrationCommand
{
    Task DeleteAsync(Guid organizationId, Guid integrationId);
}

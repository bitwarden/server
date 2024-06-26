using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections.Interfaces;

public interface IDeleteOrganizationConnectionCommand
{
    Task DeleteAsync(OrganizationConnection connection);
}

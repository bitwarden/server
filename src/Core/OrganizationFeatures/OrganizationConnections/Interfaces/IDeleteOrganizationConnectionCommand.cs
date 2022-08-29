using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationConnections.Interfaces;

public interface IDeleteOrganizationConnectionCommand
{
    Task DeleteAsync(OrganizationConnection connection);
}

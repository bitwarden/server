using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationConnections;
using Bit.Core.Models.OrganizationConnectionConfigs;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections.Interfaces;

public interface ICreateOrganizationConnectionCommand
{
    Task<OrganizationConnection> CreateAsync<T>(OrganizationConnectionData<T> connectionData)
        where T : IConnectionConfig;
}

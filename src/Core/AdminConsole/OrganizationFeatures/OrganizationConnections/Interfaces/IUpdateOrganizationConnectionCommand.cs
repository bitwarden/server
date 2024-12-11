using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationConnections;
using Bit.Core.Models.OrganizationConnectionConfigs;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections.Interfaces;

public interface IUpdateOrganizationConnectionCommand
{
    Task<OrganizationConnection> UpdateAsync<T>(OrganizationConnectionData<T> connectionData)
        where T : IConnectionConfig;
}

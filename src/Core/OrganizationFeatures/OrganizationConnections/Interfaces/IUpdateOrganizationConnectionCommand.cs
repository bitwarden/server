using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationConnections;

namespace Bit.Core.OrganizationFeatures.OrganizationConnections.Interfaces;

public interface IUpdateOrganizationConnectionCommand
{
    Task<OrganizationConnection> UpdateAsync<T>(OrganizationConnectionData<T> connectionData) where T : new();
}

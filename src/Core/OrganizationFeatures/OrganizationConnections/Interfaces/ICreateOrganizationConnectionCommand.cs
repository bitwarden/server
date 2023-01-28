using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationConnections;

namespace Bit.Core.OrganizationFeatures.OrganizationConnections.Interfaces;

public interface ICreateOrganizationConnectionCommand
{
    Task<OrganizationConnection> CreateAsync<T>(OrganizationConnectionData<T> connectionData) where T : new();
}

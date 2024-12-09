using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationConnections;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections;

public class CreateOrganizationConnectionCommand : ICreateOrganizationConnectionCommand
{
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;

    public CreateOrganizationConnectionCommand(IOrganizationConnectionRepository organizationConnectionRepository)
    {
        _organizationConnectionRepository = organizationConnectionRepository;
    }

    public async Task<OrganizationConnection> CreateAsync<T>(OrganizationConnectionData<T> connectionData) where T : IConnectionConfig
    {
        return await _organizationConnectionRepository.CreateAsync(connectionData.ToEntity());
    }
}

using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationConnections;
using Bit.Core.OrganizationFeatures.OrganizationConnections.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationConnections;

public class CreateOrganizationConnectionCommand : ICreateOrganizationConnectionCommand
{
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;

    public CreateOrganizationConnectionCommand(IOrganizationConnectionRepository organizationConnectionRepository)
    {
        _organizationConnectionRepository = organizationConnectionRepository;
    }

    public async Task<OrganizationConnection> CreateAsync<T>(OrganizationConnectionData<T> connectionData) where T : new()
    {
        return await _organizationConnectionRepository.CreateAsync(connectionData.ToEntity());
    }
}

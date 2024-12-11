﻿using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections;

public class DeleteOrganizationConnectionCommand : IDeleteOrganizationConnectionCommand
{
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;

    public DeleteOrganizationConnectionCommand(
        IOrganizationConnectionRepository organizationConnectionRepository
    )
    {
        _organizationConnectionRepository = organizationConnectionRepository;
    }

    public async Task DeleteAsync(OrganizationConnection connection)
    {
        await _organizationConnectionRepository.DeleteAsync(connection);
    }
}

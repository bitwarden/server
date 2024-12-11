﻿using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationConnections;
using Bit.Core.Models.OrganizationConnectionConfigs;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections;

public class UpdateOrganizationConnectionCommand : IUpdateOrganizationConnectionCommand
{
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;

    public UpdateOrganizationConnectionCommand(
        IOrganizationConnectionRepository organizationConnectionRepository
    )
    {
        _organizationConnectionRepository = organizationConnectionRepository;
    }

    public async Task<OrganizationConnection> UpdateAsync<T>(
        OrganizationConnectionData<T> connectionData
    )
        where T : IConnectionConfig
    {
        if (!connectionData.Id.HasValue)
        {
            throw new Exception("Cannot update connection, Connection does not exist.");
        }

        var connection = await _organizationConnectionRepository.GetByIdAsync(
            connectionData.Id.Value
        );

        if (connection == null)
        {
            throw new NotFoundException();
        }

        var entity = connectionData.ToEntity();
        await _organizationConnectionRepository.UpsertAsync(entity);
        return entity;
    }
}

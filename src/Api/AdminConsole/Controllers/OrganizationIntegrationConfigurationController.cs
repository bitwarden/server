﻿using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{organizationId:guid}/integrations/{integrationId:guid}/configurations")]
[Authorize("Application")]
public class OrganizationIntegrationConfigurationController(
    ICurrentContext currentContext,
    IOrganizationIntegrationRepository integrationRepository,
    IOrganizationIntegrationConfigurationRepository integrationConfigurationRepository) : Controller
{
    [HttpPost("")]
    public async Task<OrganizationIntegrationConfigurationResponseModel> PostAsync(
        Guid organizationId,
        Guid integrationId,
        [FromBody] OrganizationIntegrationConfigurationRequestModel model)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }
        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration == null ||
            integration.OrganizationId != organizationId ||
            model.OrganizationIntegrationId != integrationId)
        {
            throw new NotFoundException();
        }

        var organizationIntegrationConfiguration = model.ToOrganizationIntegrationConfiguration();
        var configuration = await integrationConfigurationRepository.CreateAsync(organizationIntegrationConfiguration);
        return new OrganizationIntegrationConfigurationResponseModel(configuration);
    }

    [HttpPut("{configurationId:guid}")]
    public async Task<OrganizationIntegrationConfigurationResponseModel> UpdateAsync(
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        [FromBody] OrganizationIntegrationConfigurationRequestModel model)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }
        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration == null ||
            integration.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var configuration = await integrationConfigurationRepository.GetByIdAsync(configurationId);
        if (configuration is null ||
            configuration.OrganizationIntegrationId != integrationId ||
            model.Id != configurationId ||
            model.OrganizationIntegrationId != integrationId)
        {
            throw new BadRequestException();
        }

        var newConfiguration = model.ToOrganizationIntegrationConfiguration();
        await integrationConfigurationRepository.ReplaceAsync(model.ToOrganizationIntegrationConfiguration());

        return new OrganizationIntegrationConfigurationResponseModel(newConfiguration);
    }

    [HttpDelete("{configurationId:guid}")]
    [HttpPost("{configurationId:guid}/delete")]
    public async Task DeleteAsync(Guid organizationId, Guid integrationId, Guid configurationId)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }
        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration == null || integration.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var configuration = await integrationConfigurationRepository.GetByIdAsync(configurationId);
        if (configuration is null || configuration.OrganizationIntegrationId != integrationId)
        {
            throw new NotFoundException();
        }

        await integrationConfigurationRepository.DeleteAsync(configuration);
    }
}

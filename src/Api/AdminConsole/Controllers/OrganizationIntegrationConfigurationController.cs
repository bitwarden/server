using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[RequireFeature(FeatureFlagKeys.EventBasedOrganizationIntegrations)]
[Route("organizations/{organizationId:guid}/integrations/{integrationId:guid}/configurations")]
[Authorize("Application")]
public class OrganizationIntegrationConfigurationController(
    ICurrentContext currentContext,
    IOrganizationIntegrationRepository integrationRepository,
    IOrganizationIntegrationConfigurationRepository integrationConfigurationRepository) : Controller
{
    [HttpPost("")]
    public async Task<OrganizationIntegrationConfigurationResponseModel> CreateAsync(
        Guid organizationId,
        Guid integrationId,
        [FromBody] OrganizationIntegrationConfigurationRequestModel model)
    {
        if (!await HasPermission(organizationId))
        {
            throw new NotFoundException();
        }
        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration == null || integration.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }
        if (!model.IsValidForType(integration.Type))
        {
            throw new BadRequestException($"Invalid Configuration and/or Template for integration type {integration.Type}");
        }

        var organizationIntegrationConfiguration = model.ToOrganizationIntegrationConfiguration(integrationId);
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
        if (!await HasPermission(organizationId))
        {
            throw new NotFoundException();
        }
        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration == null || integration.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }
        if (!model.IsValidForType(integration.Type))
        {
            throw new BadRequestException($"Invalid Configuration and/or Template for integration type {integration.Type}");
        }

        var configuration = await integrationConfigurationRepository.GetByIdAsync(configurationId);
        if (configuration is null || configuration.OrganizationIntegrationId != integrationId)
        {
            throw new NotFoundException();
        }

        var newConfiguration = model.ToOrganizationIntegrationConfiguration(configuration);
        await integrationConfigurationRepository.ReplaceAsync(newConfiguration);

        return new OrganizationIntegrationConfigurationResponseModel(newConfiguration);
    }

    [HttpDelete("{configurationId:guid}")]
    [HttpPost("{configurationId:guid}/delete")]
    public async Task DeleteAsync(Guid organizationId, Guid integrationId, Guid configurationId)
    {
        if (!await HasPermission(organizationId))
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

    private async Task<bool> HasPermission(Guid organizationId)
    {
        return await currentContext.OrganizationOwner(organizationId);
    }
}

using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{organizationId:guid}/integrations/{integrationId:guid}/configurations")]
[Authorize("Application")]
public class OrganizationIntegrationConfigurationController(
    ICurrentContext currentContext,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)] IFusionCache cache,
    IOrganizationIntegrationRepository integrationRepository,
    IOrganizationIntegrationConfigurationRepository integrationConfigurationRepository) : Controller
{
    [HttpGet("")]
    public async Task<List<OrganizationIntegrationConfigurationResponseModel>> GetAsync(
        Guid organizationId,
        Guid integrationId)
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

        var configurations = await integrationConfigurationRepository.GetManyByIntegrationAsync(integrationId);
        return configurations
            .Select(configuration => new OrganizationIntegrationConfigurationResponseModel(configuration))
            .ToList();
    }

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

        // Invalidate the cached configuration details
        // Even though this is a new record, the cache could hold a stale empty list for this
        await cache.RemoveAsync(
            EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId: organizationId,
                integrationType: integration.Type,
                eventType: configuration.EventType
            ));

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

        await cache.RemoveAsync(
            EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId: organizationId,
                integrationType: integration.Type,
                eventType: configuration.EventType
            ));

        return new OrganizationIntegrationConfigurationResponseModel(newConfiguration);
    }

    [HttpDelete("{configurationId:guid}")]
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
        await cache.RemoveAsync(
            EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId: organizationId,
                integrationType: integration.Type,
                eventType: configuration.EventType
            ));
    }

    [HttpPost("{configurationId:guid}/delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead")]
    public async Task PostDeleteAsync(Guid organizationId, Guid integrationId, Guid configurationId)
    {
        await DeleteAsync(organizationId, integrationId, configurationId);
    }

    private async Task<bool> HasPermission(Guid organizationId)
    {
        return await currentContext.OrganizationOwner(organizationId);
    }
}

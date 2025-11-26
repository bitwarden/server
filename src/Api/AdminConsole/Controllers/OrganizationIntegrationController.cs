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

[Route("organizations/{organizationId:guid}/integrations")]
[Authorize("Application")]
public class OrganizationIntegrationController(
    ICurrentContext currentContext,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)] IFusionCache cache,
    IOrganizationIntegrationRepository integrationRepository) : Controller
{
    [HttpGet("")]
    public async Task<List<OrganizationIntegrationResponseModel>> GetAsync(Guid organizationId)
    {
        if (!await HasPermission(organizationId))
        {
            throw new NotFoundException();
        }

        var integrations = await integrationRepository.GetManyByOrganizationAsync(organizationId);
        return integrations
            .Select(integration => new OrganizationIntegrationResponseModel(integration))
            .ToList();
    }

    [HttpPost("")]
    public async Task<OrganizationIntegrationResponseModel> CreateAsync(Guid organizationId, [FromBody] OrganizationIntegrationRequestModel model)
    {
        if (!await HasPermission(organizationId))
        {
            throw new NotFoundException();
        }

        var integrations = await integrationRepository.GetManyByOrganizationAsync(organizationId: organizationId);
        if (integrations.Any(i => i.Type == model.Type))
        {
            throw new BadRequestException("An integration of this type already exists for this organization.");
        }

        var integration = await integrationRepository.CreateAsync(model.ToOrganizationIntegration(organizationId));

        // Invalidate all cached configuration details for this integration
        // Even though this is a new record, the cache could hold a stale empty list for this
        await cache.RemoveByTagAsync(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
            organizationId: organizationId,
            integrationType: integration.Type
        ));

        return new OrganizationIntegrationResponseModel(integration);
    }

    [HttpPut("{integrationId:guid}")]
    public async Task<OrganizationIntegrationResponseModel> UpdateAsync(Guid organizationId, Guid integrationId, [FromBody] OrganizationIntegrationRequestModel model)
    {
        if (!await HasPermission(organizationId))
        {
            throw new NotFoundException();
        }

        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration is null || integration.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        await integrationRepository.ReplaceAsync(model.ToOrganizationIntegration(integration));

        // Invalidate all cached configuration details for this integration
        await cache.RemoveByTagAsync(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
            organizationId: organizationId,
            integrationType: integration.Type
        ));

        return new OrganizationIntegrationResponseModel(integration);
    }

    [HttpDelete("{integrationId:guid}")]
    public async Task DeleteAsync(Guid organizationId, Guid integrationId)
    {
        if (!await HasPermission(organizationId))
        {
            throw new NotFoundException();
        }

        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration is null || integration.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        await integrationRepository.DeleteAsync(integration);

        // Invalidate all cached configuration details for this integration
        await cache.RemoveByTagAsync(EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
            organizationId: organizationId,
            integrationType: integration.Type
        ));
    }

    [HttpPost("{integrationId:guid}/delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead")]
    public async Task PostDeleteAsync(Guid organizationId, Guid integrationId)
    {
        await DeleteAsync(organizationId, integrationId);
    }

    private async Task<bool> HasPermission(Guid organizationId)
    {
        return await currentContext.OrganizationOwner(organizationId);
    }
}

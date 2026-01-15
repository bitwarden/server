using Bit.Api.Dirt.Models.Request;
using Bit.Api.Dirt.Models.Response;
using Bit.Core.Context;
using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

[Route("organizations/{organizationId:guid}/integrations/{integrationId:guid}/configurations")]
[Authorize("Application")]
public class OrganizationIntegrationConfigurationController(
    ICurrentContext currentContext,
    ICreateOrganizationIntegrationConfigurationCommand createCommand,
    IUpdateOrganizationIntegrationConfigurationCommand updateCommand,
    IDeleteOrganizationIntegrationConfigurationCommand deleteCommand,
    IGetOrganizationIntegrationConfigurationsQuery getQuery) : Controller
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

        var configurations = await getQuery.GetManyByIntegrationAsync(organizationId, integrationId);
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

        var configuration = model.ToOrganizationIntegrationConfiguration(integrationId);
        var created = await createCommand.CreateAsync(organizationId, integrationId, configuration);

        return new OrganizationIntegrationConfigurationResponseModel(created);
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

        var configuration = model.ToOrganizationIntegrationConfiguration(integrationId);
        var updated = await updateCommand.UpdateAsync(organizationId, integrationId, configurationId, configuration);

        return new OrganizationIntegrationConfigurationResponseModel(updated);
    }

    [HttpDelete("{configurationId:guid}")]
    public async Task DeleteAsync(Guid organizationId, Guid integrationId, Guid configurationId)
    {
        if (!await HasPermission(organizationId))
        {
            throw new NotFoundException();
        }

        await deleteCommand.DeleteAsync(organizationId, integrationId, configurationId);
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

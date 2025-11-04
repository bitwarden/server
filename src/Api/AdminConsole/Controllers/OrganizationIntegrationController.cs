using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#nullable enable

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{organizationId:guid}/integrations")]
[Authorize("Application")]
public class OrganizationIntegrationController(
    ICurrentContext currentContext,
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

        var integration = await integrationRepository.CreateAsync(model.ToOrganizationIntegration(organizationId));
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

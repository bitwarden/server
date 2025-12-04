using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations.Interfaces;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{organizationId:guid}/integrations")]
[Authorize("Application")]
public class OrganizationIntegrationController(
    ICurrentContext currentContext,
    ICreateOrganizationIntegrationCommand createCommand,
    IUpdateOrganizationIntegrationCommand updateCommand,
    IDeleteOrganizationIntegrationCommand deleteCommand,
    IGetOrganizationIntegrationsQuery getQuery) : Controller
{
    [HttpGet("")]
    public async Task<List<OrganizationIntegrationResponseModel>> GetAsync(Guid organizationId)
    {
        if (!await HasPermission(organizationId))
        {
            throw new NotFoundException();
        }

        var integrations = await getQuery.GetManyByOrganizationAsync(organizationId);
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

        var integration = model.ToOrganizationIntegration(organizationId);
        var created = await createCommand.CreateAsync(integration);

        return new OrganizationIntegrationResponseModel(created);
    }

    [HttpPut("{integrationId:guid}")]
    public async Task<OrganizationIntegrationResponseModel> UpdateAsync(Guid organizationId, Guid integrationId, [FromBody] OrganizationIntegrationRequestModel model)
    {
        if (!await HasPermission(organizationId))
        {
            throw new NotFoundException();
        }

        var integration = model.ToOrganizationIntegration(organizationId);
        var updated = await updateCommand.UpdateAsync(organizationId, integrationId, integration);

        return new OrganizationIntegrationResponseModel(updated);
    }

    [HttpDelete("{integrationId:guid}")]
    public async Task DeleteAsync(Guid organizationId, Guid integrationId)
    {
        if (!await HasPermission(organizationId))
        {
            throw new NotFoundException();
        }

        await deleteCommand.DeleteAsync(organizationId, integrationId);
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

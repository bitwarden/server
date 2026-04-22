using Bit.Api.Dirt.Models.Request;
using Bit.Api.Dirt.Models.Response;
using Bit.Core.Context;
using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

[Route("organizations/{organizationId:guid}/integrations")]
[Authorize("Application")]
public class OrganizationIntegrationController(
    ICurrentContext currentContext,
    ICreateOrganizationIntegrationCommand createCommand,
    IUpdateOrganizationIntegrationCommand updateCommand,
    IDeleteOrganizationIntegrationCommand deleteCommand,
    IGetOrganizationIntegrationsQuery getQuery,
    ILogger<OrganizationIntegrationController> logger) : Controller
{
    [HttpGet("")]
    public async Task<ActionResult<List<OrganizationIntegrationResponseModel>>> GetAsync(Guid organizationId)
    {
        if (!await HasPermission(organizationId))
        {
            return NotFound();
        }

        var integrations = await getQuery.GetManyByOrganizationAsync(organizationId);
        return Ok(integrations
            .Select(integration => new OrganizationIntegrationResponseModel(integration))
            .ToList());
    }

    /// <summary>
    /// Creates a new organization integration. 
    /// Validates that only one integration of each type can exist per organization.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    /// <exception cref="UnauthorizedResult">Not enough permissions to access the organization.</exception>
    /// <exception cref="ConflictResult">When an integration of the same type already exists for the organization.</exception>
    [HttpPost("")]
    public async Task<ActionResult<OrganizationIntegrationResponseModel>> CreateAsync(Guid organizationId, [FromBody] OrganizationIntegrationRequestModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!await HasPermission(organizationId))
        {
            return NotFound();
        }

        var integration = model.ToOrganizationIntegration(organizationId);

        var canCreate = await createCommand.CanCreateAsync(integration);
        if (!canCreate)
        {
            return Conflict();
        }

        try
        {
            var created = await createCommand.CreateAsync(integration);
            return Ok(new OrganizationIntegrationResponseModel(created));
        }
        catch (System.Exception e)
        {   
            logger.LogError(e, "Error creating organization integration for organization {OrganizationId} with type {IntegrationType}", organizationId, integration.Type);
            return BadRequest();
        }
    }

    [HttpPut("{integrationId:guid}")]
    public async Task<ActionResult<OrganizationIntegrationResponseModel>> UpdateAsync(Guid organizationId, Guid integrationId, [FromBody] OrganizationIntegrationRequestModel model)
    {
        if (!await HasPermission(organizationId))
        {
            return NotFound();
        }

        try
        {
            var integration = model.ToOrganizationIntegration(organizationId);
            var updated = await updateCommand.UpdateAsync(organizationId, integrationId, integration);
    
            return Ok(new OrganizationIntegrationResponseModel(updated));
        }
        catch (System.Exception e)
        {
            logger.LogError(e, "Error updating organization integration for organization {OrganizationId} with integration {IntegrationId}", organizationId, integrationId);
            return BadRequest();
        }
    }

    [HttpDelete("{integrationId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid organizationId, Guid integrationId)
    {
        if (!await HasPermission(organizationId))
        {
            return NotFound();
        }

        try
        {
            await deleteCommand.DeleteAsync(organizationId, integrationId);
        }
        catch (System.Exception e)
        {
            logger.LogError(e, "Error deleting organization integration for organization {OrganizationId} with integration {IntegrationId}", organizationId, integrationId);
            return BadRequest();
        }
        return NoContent();
    }

    [HttpPost("{integrationId:guid}/delete")]
    [Obsolete("This endpoint is deprecated. Use DELETE method instead")]
    public async Task<IActionResult> PostDeleteAsync(Guid organizationId, Guid integrationId)
    {
        return await DeleteAsync(organizationId, integrationId);
    }

    private async Task<bool> HasPermission(Guid organizationId)
    {
        return await currentContext.OrganizationOwner(organizationId);
    }
}

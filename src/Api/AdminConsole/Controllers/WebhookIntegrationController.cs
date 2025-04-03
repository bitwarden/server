using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{organizationId:guid}/integrations/webhook/")]
[Authorize("Application")]
public class WebhookIntegrationController(
    ICurrentContext currentContext,
    IOrganizationIntegrationRepository integrationRepository) : Controller
{
    [HttpGet("create")]
    public async Task<IActionResult> CreateAsync(Guid organizationId)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }

        var integration = await integrationRepository.CreateAsync(new OrganizationIntegration
        {
            OrganizationId = organizationId,
            Type = IntegrationType.Webhook,
            Configuration = null
        });
        return Ok(new { id = integration.Id });
    }

    [HttpDelete("{integrationId:guid}")]
    [HttpPost("{integrationId:guid}/delete")]
    public async Task DeleteAsync(Guid organizationId, Guid integrationId)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }

        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration is null)
        {
            throw new NotFoundException();
        }

        await integrationRepository.DeleteAsync(integration);
    }
}

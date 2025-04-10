using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{organizationId:guid}/integrations/slack/")]
[Authorize("Application")]
public class SlackIntegrationController(
    ICurrentContext currentContext,
    IOrganizationIntegrationRepository integrationRepository,
    ISlackService slackService) : Controller
{
    [HttpGet("redirect")]
    public async Task<IActionResult> RedirectAsync(Guid organizationId)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }
        string callbackUrl = Url.RouteUrl(
            nameof(CreateAsync),
            new { organizationId },
            currentContext.HttpContext.Request.Scheme);
        var redirectUrl = slackService.GetRedirectUrl(callbackUrl);

        if (string.IsNullOrEmpty(redirectUrl))
        {
            throw new NotFoundException();
        }

        return Redirect(redirectUrl);
    }

    [HttpGet("create", Name = nameof(CreateAsync))]
    public async Task<IActionResult> CreateAsync(Guid organizationId, [FromQuery] string code)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }

        if (string.IsNullOrEmpty(code))
        {
            throw new BadRequestException("Missing code from Slack.");
        }

        string callbackUrl = Url.RouteUrl(
            nameof(CreateAsync),
            new { organizationId },
            currentContext.HttpContext.Request.Scheme);
        var token = await slackService.ObtainTokenViaOAuth(code, callbackUrl);

        if (string.IsNullOrEmpty(token))
        {
            throw new BadRequestException("Invalid response from Slack.");
        }

        var integration = await integrationRepository.CreateAsync(new OrganizationIntegration
        {
            OrganizationId = organizationId,
            Type = IntegrationType.Slack,
            Configuration = JsonSerializer.Serialize(new SlackIntegration(token)),
        });
        return Ok(new { id = integration.Id });
    }
}

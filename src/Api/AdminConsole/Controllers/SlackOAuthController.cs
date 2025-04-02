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

[Route("slack/oauth")]
[Authorize("Application")]
public class SlackOAuthController(
    ICurrentContext currentContext,
    IOrganizationIntegrationRepository integrationRepository,
    ISlackService slackService) : Controller
{
    [HttpGet("redirect/{id:guid}")]
    public async Task<IActionResult> RedirectToSlack(Guid id)
    {
        if (!await currentContext.OrganizationOwner(id))
        {
            throw new NotFoundException();
        }
        string callbackUrl = Url.RouteUrl(nameof(OAuthCallback), new { id = id }, currentContext.HttpContext.Request.Scheme);
        var redirectUrl = slackService.GetRedirectUrl(callbackUrl);

        if (string.IsNullOrEmpty(redirectUrl))
        {
            throw new NotFoundException();
        }

        return Redirect(redirectUrl);
    }

    [HttpGet("callback/{id:guid}", Name = nameof(OAuthCallback))]
    public async Task<IActionResult> OAuthCallback(Guid id, [FromQuery] string code)
    {
        if (!await currentContext.OrganizationOwner(id))
        {
            throw new NotFoundException();
        }

        if (string.IsNullOrEmpty(code))
        {
            throw new BadRequestException("Missing code from Slack.");
        }

        string callbackUrl = Url.RouteUrl(nameof(OAuthCallback), new { id = id }, currentContext.HttpContext.Request.Scheme);
        var token = await slackService.ObtainTokenViaOAuth(code, callbackUrl);

        if (string.IsNullOrEmpty(token))
        {
            throw new BadRequestException("Invalid response from Slack.");
        }

        var integration = await integrationRepository.CreateAsync(new OrganizationIntegration
        {
            OrganizationId = id,
            Type = IntegrationType.Slack,
            Configuration = JsonSerializer.Serialize(new SlackIntegration(token)),
        });
        return Ok(integration.Id);
    }
}

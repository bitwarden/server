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
    IOrganizationIntegrationConfigurationRepository integrationConfigurationRepository,
    ISlackService slackService) : Controller
{
    [HttpGet("redirect/{id}")]
    public async Task<IActionResult> RedirectToSlack(string id)
    {
        var orgIdGuid = new Guid(id);
        if (!await currentContext.OrganizationOwner(orgIdGuid))
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

    [HttpGet("callback/{id}", Name = nameof(OAuthCallback))]
    public async Task<IActionResult> OAuthCallback(string id, [FromQuery] string code)
    {
        var orgIdGuid = new Guid(id);
        if (!await currentContext.OrganizationOwner(orgIdGuid))
        {
            throw new NotFoundException();
        }

        if (string.IsNullOrEmpty(code))
        {
            throw new BadRequestException("Missing code from Slack.");
        }

        string callbackUrl = Url.RouteUrl(nameof(OAuthCallback));
        var token = await slackService.ObtainTokenViaOAuth(code, callbackUrl);

        if (string.IsNullOrEmpty(token))
        {
            throw new BadRequestException("Invalid response from Slack.");
        }

        await integrationConfigurationRepository.CreateOrganizationIntegrationAsync(
            orgIdGuid,
            IntegrationType.Slack,
            new SlackIntegration(token));
        return Ok("Slack OAuth successful. Your bot is now installed.");
    }
}

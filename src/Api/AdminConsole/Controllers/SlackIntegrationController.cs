// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[RequireFeature(FeatureFlagKeys.EventBasedOrganizationIntegrations)]
[Route("organizations/{organizationId:guid}/integrations/slack")]
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
        var location = $"/organizations/{organizationId}/integrations/{integration.Id}";

        return Created(location, new OrganizationIntegrationResponseModel(integration));
    }
}

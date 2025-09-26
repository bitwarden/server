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
[Route("organizations")]
[Authorize("Application")]
public class SlackIntegrationController(
    ICurrentContext currentContext,
    IOrganizationIntegrationRepository integrationRepository,
    ISlackService slackService) : Controller
{
    [HttpGet("{organizationId:guid}/integrations/slack/redirect")]
    public async Task<IActionResult> RedirectAsync(Guid organizationId)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }

        string? callbackUrl = Url.RouteUrl(
            routeName: nameof(CreateAsync),
            values: null,
            protocol: currentContext.HttpContext.Request.Scheme,
            host: currentContext.HttpContext.Request.Host.ToUriComponent()
        );
        if (string.IsNullOrEmpty(callbackUrl))
        {
            throw new BadRequestException("Unable to build callback Url");
        }

        var integrations = await integrationRepository.GetManyByOrganizationAsync(organizationId);
        var integration = integrations.FirstOrDefault(i => i.Type == IntegrationType.Slack);

        if (integration is null)
        {
            integration = await integrationRepository.CreateAsync(new OrganizationIntegration
            {
                OrganizationId = organizationId,
                Type = IntegrationType.Slack,
                Configuration = null,
            });
        }
        if (integration.Configuration is not null)
        {
            throw new BadRequestException("There already exists a Slack integration for this organization");
        }

        var redirectUrl = slackService.GetRedirectUrl(
            callbackUrl: callbackUrl,
            state: integration.Id.ToString()
        );

        if (string.IsNullOrEmpty(redirectUrl))
        {
            throw new NotFoundException();
        }

        return Redirect(redirectUrl);
    }

    [HttpGet("integrations/slack/create", Name = nameof(CreateAsync))]
    [AllowAnonymous]
    public async Task<IActionResult> CreateAsync([FromQuery] string code, [FromQuery] string state)
    {
        // Fetch existing Initiated record
        var integration = await integrationRepository.GetByIdAsync(Guid.Parse(state));
        if (integration is null)
        {
            throw new BadRequestException("No record found for given state.");
        }

        // Fetch token from Slack and store to DB
        string? callbackUrl = Url.RouteUrl(
            routeName: nameof(CreateAsync),
            values: null,
            protocol: currentContext.HttpContext.Request.Scheme,
            host: currentContext.HttpContext.Request.Host.ToUriComponent()
        );
        if (string.IsNullOrEmpty(callbackUrl))
        {
            throw new BadRequestException("Unable to build callback Url");
        }
        var token = await slackService.ObtainTokenViaOAuth(code, callbackUrl);

        if (string.IsNullOrEmpty(token))
        {
            throw new BadRequestException("Invalid response from Slack.");
        }

        integration.Configuration = JsonSerializer.Serialize(new SlackIntegration(token));
        await integrationRepository.UpsertAsync(integration);

        var location = $"/organizations/{integration.OrganizationId}/integrations/{integration.Id}";
        return Created(location, new OrganizationIntegrationResponseModel(integration));
    }
}

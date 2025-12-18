using System.Text.Json;
using Bit.Api.Dirt.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

[Route("organizations")]
[Authorize("Application")]
public class SlackIntegrationController(
    ICurrentContext currentContext,
    IOrganizationIntegrationRepository integrationRepository,
    ISlackService slackService,
    TimeProvider timeProvider) : Controller
{
    [HttpGet("{organizationId:guid}/integrations/slack/redirect")]
    public async Task<IActionResult> RedirectAsync(Guid organizationId)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }

        string? callbackUrl = Url.RouteUrl(
            routeName: "SlackIntegration_Create",
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
            // No slack integration exists, create Initiated version
            integration = await integrationRepository.CreateAsync(new OrganizationIntegration
            {
                OrganizationId = organizationId,
                Type = IntegrationType.Slack,
                Configuration = null,
            });
        }
        else if (integration.Configuration is not null)
        {
            // A Completed (fully configured) Slack integration already exists, throw to prevent overriding
            throw new BadRequestException("There already exists a Slack integration for this organization");

        } // An Initiated slack integration exits, re-use it and kick off a new OAuth flow

        var state = IntegrationOAuthState.FromIntegration(integration, timeProvider);
        var redirectUrl = slackService.GetRedirectUrl(
            callbackUrl: callbackUrl,
            state: state.ToString()
        );

        if (string.IsNullOrEmpty(redirectUrl))
        {
            throw new NotFoundException();
        }

        return Redirect(redirectUrl);
    }

    [HttpGet("integrations/slack/create", Name = "SlackIntegration_Create")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateAsync([FromQuery] string code, [FromQuery] string state)
    {
        var oAuthState = IntegrationOAuthState.FromString(state: state, timeProvider: timeProvider);
        if (oAuthState is null)
        {
            throw new NotFoundException();
        }

        // Fetch existing Initiated record
        var integration = await integrationRepository.GetByIdAsync(oAuthState.IntegrationId);
        if (integration is null ||
            integration.Type != IntegrationType.Slack ||
            integration.Configuration is not null)
        {
            throw new NotFoundException();
        }

        // Verify Organization matches hash
        if (!oAuthState.ValidateOrg(integration.OrganizationId))
        {
            throw new NotFoundException();
        }

        // Fetch token from Slack and store to DB
        string? callbackUrl = Url.RouteUrl(
            routeName: "SlackIntegration_Create",
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

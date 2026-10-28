using System.Text.Json;
using Bit.Api.Dirt.Models.Response;
using Bit.Api.Dirt.Utilities;
using Bit.Core.Context;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services;
using Bit.Core.Exceptions;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

[Route("organizations")]
[Authorize("Application")]
public class SlackIntegrationController(
    ICurrentContext currentContext,
    IOrganizationIntegrationRepository integrationRepository,
    ISlackService slackService,
    IGlobalSettings globalSettings,
    IDataProtectorTokenFactory<IntegrationOAuthStateTokenable> stateFactory,
    TimeProvider timeProvider) : Controller
{
    [HttpGet("{organizationId:guid}/integrations/slack/redirect")]
    public async Task<OrganizationIntegrationOAuthRedirectResponseModel> RedirectAsync(Guid organizationId)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }

        string? callbackUrl = BuildCallbackUrl();
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

        var state = stateFactory.Protect(new IntegrationOAuthStateTokenable(integration, timeProvider));
        var redirectUrl = slackService.GetRedirectUrl(
            callbackUrl: callbackUrl,
            state: state
        );

        if (string.IsNullOrEmpty(redirectUrl))
        {
            throw new NotFoundException();
        }

        return new OrganizationIntegrationOAuthRedirectResponseModel(redirectUrl);
    }

    [HttpGet("integrations/slack/create", Name = "SlackIntegration_Create")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateAsync(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        // The state is protected server-side, so a valid one could only have come from RedirectAsync
        if (string.IsNullOrEmpty(state) ||
            !stateFactory.TryUnprotect(state, out var oAuthState) ||
            !oAuthState.Valid ||
            oAuthState.Type != IntegrationType.Slack)
        {
            throw new NotFoundException();
        }

        // Fetch existing Initiated record. Only redirect to the web vault once the state matches it.
        var integration = await integrationRepository.GetByIdAsync(oAuthState.IntegrationId);
        if (integration is null || !oAuthState.IsValidFor(integration))
        {
            throw new NotFoundException();
        }

        if (integration.Configuration is not null)
        {
            return RedirectToVault(integration, IntegrationOAuthCallbackRedirect.AlreadyConnected);
        }

        // Slack returns "error" instead of "code" when the user declines consent
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            return RedirectToVault(integration, IntegrationOAuthCallbackRedirect.Canceled);
        }

        // Fetch token from Slack and store to DB
        string? callbackUrl = BuildCallbackUrl();
        if (string.IsNullOrEmpty(callbackUrl))
        {
            return RedirectToVault(integration, IntegrationOAuthCallbackRedirect.Failed);
        }
        var token = await slackService.ObtainTokenViaOAuth(code, callbackUrl);

        if (string.IsNullOrEmpty(token))
        {
            return RedirectToVault(integration, IntegrationOAuthCallbackRedirect.Failed);
        }

        integration.Configuration = JsonSerializer.Serialize(new SlackIntegration(token));
        await integrationRepository.UpsertAsync(integration);

        return RedirectToVault(integration, IntegrationOAuthCallbackRedirect.Success);
    }

    private RedirectResult RedirectToVault(OrganizationIntegration integration, string result) =>
        Redirect(IntegrationOAuthCallbackRedirect.BuildUrl(
            globalSettings, integration.OrganizationId, IntegrationType.Slack, result));

    /// <summary>
    /// Builds the OAuth callback URL from the configured API base URL rather than the incoming request.
    /// In cloud, TLS terminates before the request reaches the API, so the request scheme is "http" and
    /// Slack rejects the redirect URI. Self-hosted instances also need the "/api" prefix that the
    /// reverse proxy strips before the request reaches the API.
    /// </summary>
    private string? BuildCallbackUrl()
    {
        var apiBaseUrl = globalSettings.BaseServiceUri.Api;
        var callbackPath = Url.RouteUrl(routeName: "SlackIntegration_Create", values: null);
        if (string.IsNullOrEmpty(apiBaseUrl) || string.IsNullOrEmpty(callbackPath))
        {
            return null;
        }

        return $"{apiBaseUrl.TrimEnd('/')}{callbackPath}";
    }
}

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
using Bit.HttpExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;

namespace Bit.Api.Dirt.Controllers;

[Route("organizations")]
[Authorize("Application")]
public class TeamsIntegrationController(
    ICurrentContext currentContext,
    IOrganizationIntegrationRepository integrationRepository,
    IBot bot,
    IBotFrameworkHttpAdapter adapter,
    ITeamsService teamsService,
    IGlobalSettings globalSettings,
    TimeProvider timeProvider) : Controller
{
    [HttpGet("{organizationId:guid}/integrations/teams/redirect")]
    public async Task<OrganizationIntegrationOAuthRedirectResponseModel> RedirectAsync(Guid organizationId)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }

        var callbackUrl = BuildCallbackUrl();
        if (string.IsNullOrEmpty(callbackUrl))
        {
            throw new BadRequestException("Unable to build callback Url");
        }

        var integrations = await integrationRepository.GetManyByOrganizationAsync(organizationId);
        var integration = integrations.FirstOrDefault(i => i.Type == IntegrationType.Teams);

        if (integration is null)
        {
            // No teams integration exists, create Initiated version
            integration = await integrationRepository.CreateAsync(new OrganizationIntegration
            {
                OrganizationId = organizationId,
                Type = IntegrationType.Teams,
                Configuration = null,
            });
        }
        else if (integration.Configuration is not null)
        {
            // A Completed (fully configured) Teams integration already exists, throw to prevent overriding
            throw new BadRequestException("There already exists a Teams integration for this organization");

        } // An Initiated teams integration exits, re-use it and kick off a new OAuth flow

        var state = IntegrationOAuthState.FromIntegration(integration, timeProvider);
        var redirectUrl = teamsService.GetRedirectUrl(
            callbackUrl: callbackUrl,
            state: state.ToString()
        );

        if (string.IsNullOrEmpty(redirectUrl))
        {
            throw new NotFoundException();
        }

        return new OrganizationIntegrationOAuthRedirectResponseModel(redirectUrl);
    }

    [HttpGet("integrations/teams/create", Name = "TeamsIntegration_Create")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateAsync(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        var oAuthState = state is null ? null : IntegrationOAuthState.FromString(state: state, timeProvider: timeProvider);
        if (oAuthState is null)
        {
            throw new NotFoundException();
        }

        // Fetch existing Initiated record
        var integration = await integrationRepository.GetByIdAsync(oAuthState.IntegrationId);
        if (integration is null || integration.Type != IntegrationType.Teams)
        {
            throw new NotFoundException();
        }

        // Verify Organization matches hash. Only redirect to the web vault once the organization is trusted.
        if (!oAuthState.ValidateOrg(integration.OrganizationId))
        {
            throw new NotFoundException();
        }

        if (integration.Configuration is not null)
        {
            return RedirectToVault(integration, IntegrationOAuthCallbackRedirect.AlreadyConnected);
        }

        // Microsoft returns "error" instead of "code" when the user declines consent
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            return RedirectToVault(integration, IntegrationOAuthCallbackRedirect.Canceled);
        }

        var callbackUrl = BuildCallbackUrl();
        if (string.IsNullOrEmpty(callbackUrl))
        {
            return RedirectToVault(integration, IntegrationOAuthCallbackRedirect.Failed);
        }

        var token = await teamsService.ObtainTokenViaOAuth(code, callbackUrl);
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToVault(integration, IntegrationOAuthCallbackRedirect.Failed);
        }

        var teams = await teamsService.GetJoinedTeamsAsync(token);

        if (!teams.Any())
        {
            return RedirectToVault(integration, IntegrationOAuthCallbackRedirect.NoTeams);
        }

        var teamsIntegration = new TeamsIntegration(TenantId: teams[0].TenantId, Teams: teams);
        integration.Configuration = JsonSerializer.Serialize(teamsIntegration);
        await integrationRepository.UpsertAsync(integration);

        return RedirectToVault(integration, IntegrationOAuthCallbackRedirect.Success);
    }

    [HttpGet("{organizationId:guid}/integrations/{integrationId:guid}/teams/channels")]
    public async Task<ListResponseModel<TeamsChannelResponseModel>> GetChannelsAsync(
        Guid organizationId,
        Guid integrationId)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }

        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration is null ||
            integration.OrganizationId != organizationId ||
            integration.Type != IntegrationType.Teams)
        {
            throw new NotFoundException();
        }

        // The install conversation ID is the team's ID for the Bot Framework.
        var teamsIntegration = integration.Configuration is null
            ? null
            : JsonSerializer.Deserialize<TeamsIntegration>(integration.Configuration);
        if (teamsIntegration is not { ChannelId: { } teamId, ServiceUrl: { } serviceUrl })
        {
            throw new BadRequestException("The Bitwarden app has not been added to a team yet.");
        }

        var channels = await teamsService.GetStandardChannelsAsync(serviceUrl, teamId)
            ?? throw new BadRequestException("Unable to retrieve the channels for the connected team. Please try again.");

        return new ListResponseModel<TeamsChannelResponseModel>(
            channels.Select(channel => new TeamsChannelResponseModel(channel)));
    }

    [Route("integrations/teams/incoming")]
    [AllowAnonymous]
    [HttpPost]
    public async Task IncomingPostAsync()
    {
        await adapter.ProcessAsync(Request, Response, bot);
    }

    private RedirectResult RedirectToVault(OrganizationIntegration integration, string result) =>
        Redirect(IntegrationOAuthCallbackRedirect.BuildUrl(
            globalSettings, integration.OrganizationId, IntegrationType.Teams, result));

    /// <summary>
    /// Builds the OAuth callback URL from the configured API base URL rather than the incoming request.
    /// In cloud, TLS terminates before the request reaches the API, so the request scheme is "http" and
    /// Microsoft Entra ID rejects the redirect URI. Self-hosted instances also need the "/api" prefix that the
    /// reverse proxy strips before the request reaches the API.
    /// </summary>
    private string? BuildCallbackUrl()
    {
        var apiBaseUrl = globalSettings.BaseServiceUri.Api;
        var callbackPath = Url.RouteUrl(routeName: "TeamsIntegration_Create", values: null);
        if (string.IsNullOrEmpty(apiBaseUrl) || string.IsNullOrEmpty(callbackPath))
        {
            return null;
        }

        return $"{apiBaseUrl.TrimEnd('/')}{callbackPath}";
    }
}

using System.Text.Json;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations")]
[Authorize("Application")]
public class TeamsIntegrationController(
    ICurrentContext currentContext,
    IOrganizationIntegrationRepository integrationRepository,
    IBot bot,
    IBotFrameworkHttpAdapter adapter,
    ITeamsService teamsService,
    TimeProvider timeProvider) : Controller
{
    [HttpGet("{organizationId:guid}/integrations/teams/redirect")]
    public async Task<IActionResult> RedirectAsync(Guid organizationId)
    {
        if (!await currentContext.OrganizationOwner(organizationId))
        {
            throw new NotFoundException();
        }

        var callbackUrl = Url.RouteUrl(
            routeName: "TeamsIntegration_Create",
            values: null,
            protocol: currentContext.HttpContext.Request.Scheme,
            host: currentContext.HttpContext.Request.Host.ToUriComponent()
        );
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

        return Redirect(redirectUrl);
    }

    [HttpGet("integrations/teams/create", Name = "TeamsIntegration_Create")]
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
            integration.Type != IntegrationType.Teams ||
            integration.Configuration is not null)
        {
            throw new NotFoundException();
        }

        // Verify Organization matches hash
        if (!oAuthState.ValidateOrg(integration.OrganizationId))
        {
            throw new NotFoundException();
        }

        var callbackUrl = Url.RouteUrl(
            routeName: "TeamsIntegration_Create",
            values: null,
            protocol: currentContext.HttpContext.Request.Scheme,
            host: currentContext.HttpContext.Request.Host.ToUriComponent()
        );
        if (string.IsNullOrEmpty(callbackUrl))
        {
            throw new BadRequestException("Unable to build callback Url");
        }

        var token = await teamsService.ObtainTokenViaOAuth(code, callbackUrl);
        if (string.IsNullOrEmpty(token))
        {
            throw new BadRequestException("Invalid response from Teams.");
        }

        var teams = await teamsService.GetJoinedTeamsAsync(token);

        if (!teams.Any())
        {
            throw new BadRequestException("No teams were found.");
        }

        var teamsIntegration = new TeamsIntegration(TenantId: teams[0].TenantId, Teams: teams);
        integration.Configuration = JsonSerializer.Serialize(teamsIntegration);
        await integrationRepository.UpsertAsync(integration);

        var location = $"/organizations/{integration.OrganizationId}/integrations/{integration.Id}";
        return Created(location, new OrganizationIntegrationResponseModel(integration));
    }

    [Route("integrations/teams/incoming")]
    [AllowAnonymous]
    [HttpPost]
    public async Task IncomingPostAsync()
    {
        await adapter.ProcessAsync(Request, Response, bot);
    }
}

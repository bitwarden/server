using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Models.Data.Teams;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using TeamInfo = Bit.Core.Dirt.Models.Data.Teams.TeamInfo;

namespace Bit.Core.Dirt.Services.Implementations;

public class TeamsService(
    IHttpClientFactory httpClientFactory,
    IOrganizationIntegrationRepository integrationRepository,
    GlobalSettings globalSettings,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)] IFusionCache cache,
    TimeProvider timeProvider,
    ILogger<TeamsService> logger) : ActivityHandler, ITeamsService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);
    private readonly string _clientId = globalSettings.Teams.ClientId;
    private readonly string _clientSecret = globalSettings.Teams.ClientSecret;
    private readonly string _scopes = globalSettings.Teams.Scopes;
    private readonly string _graphBaseUrl = globalSettings.Teams.GraphBaseUrl;
    private readonly string _loginBaseUrl = globalSettings.Teams.LoginBaseUrl;

    public const string HttpClientName = "TeamsServiceHttpClient";

    public string GetRedirectUrl(string redirectUrl, string state)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = _clientId;
        query["response_type"] = "code";
        query["redirect_uri"] = redirectUrl;
        query["response_mode"] = "query";
        query["scope"] = string.Join(" ", _scopes);
        query["state"] = state;

        return $"{_loginBaseUrl}/common/oauth2/v2.0/authorize?{query}";
    }

    public async Task<string> ObtainTokenViaOAuth(string code, string redirectUrl)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrWhiteSpace(redirectUrl))
        {
            logger.LogError("Error obtaining token via OAuth: Code and/or RedirectUrl were empty");
            return string.Empty;
        }

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_loginBaseUrl}/common/oauth2/v2.0/token");

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", _clientId },
            { "client_secret", _clientSecret },
            { "code", code },
            { "redirect_uri", redirectUrl },
            { "grant_type", "authorization_code" }
        });

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            logger.LogError("Teams OAuth token exchange failed: {errorText}", errorText);
            return string.Empty;
        }

        TeamsOAuthResponse? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<TeamsOAuthResponse>();
        }
        catch
        {
            result = null;
        }

        if (result is null)
        {
            logger.LogError("Error obtaining token via OAuth: Unknown error");
            return string.Empty;
        }

        return result.AccessToken;
    }

    public async Task<IReadOnlyList<TeamInfo>> GetJoinedTeamsAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_graphBaseUrl}/me/joinedTeams");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            logger.LogError("Get Teams request failed: {errorText}", errorText);
            return new List<TeamInfo>();
        }

        var result = await response.Content.ReadFromJsonAsync<JoinedTeamsResponse>();

        return result?.Value ?? [];
    }

    public async Task SendMessageToChannelAsync(Uri serviceUri, string channelId, string message)
    {
        var credentials = new MicrosoftAppCredentials(_clientId, _clientSecret);
        using var connectorClient = new ConnectorClient(serviceUri, credentials);

        var activity = new Activity
        {
            Type = ActivityTypes.Message,
            Text = message
        };

        await connectorClient.Conversations.SendToConversationAsync(channelId, activity);
    }

    protected override async Task OnInstallationUpdateAddAsync(ITurnContext<IInstallationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        var conversationId = turnContext.Activity.Conversation.Id;
        var serviceUrl = turnContext.Activity.ServiceUrl;
        var teamId = turnContext.Activity.TeamsGetTeamInfo().AadGroupId;
        var tenantId = turnContext.Activity.Conversation.TenantId;

        if (!string.IsNullOrWhiteSpace(conversationId) &&
            !string.IsNullOrWhiteSpace(serviceUrl) &&
            Uri.TryCreate(serviceUrl, UriKind.Absolute, out var parsedUri) &&
            !string.IsNullOrWhiteSpace(teamId) &&
            !string.IsNullOrWhiteSpace(tenantId))
        {
            await HandleIncomingAppInstallAsync(
                conversationId: conversationId,
                serviceUrl: parsedUri,
                teamId: teamId,
                tenantId: tenantId
            );
        }

        await base.OnInstallationUpdateAddAsync(turnContext, cancellationToken);
    }

    protected override async Task OnInstallationUpdateRemoveAsync(
        ITurnContext<IInstallationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        await DisconnectFromActivityAsync(turnContext.Activity);

        await base.OnInstallationUpdateRemoveAsync(turnContext, cancellationToken);
    }

    protected override async Task OnConversationUpdateActivityAsync(
        ITurnContext<IConversationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        // Inspected here rather than in OnMembersRemovedAsync because ActivityHandler only dispatches that
        // override when a member other than the bot was removed, which excludes the case we care about.
        var botId = turnContext.Activity.Recipient?.Id;
        var membersRemoved = turnContext.Activity.MembersRemoved;

        if (!string.IsNullOrWhiteSpace(botId) &&
            membersRemoved is not null &&
            membersRemoved.Any(member => member.Id == botId))
        {
            await DisconnectFromActivityAsync(turnContext.Activity);
        }

        await base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
    }

    internal async Task HandleIncomingAppInstallAsync(
        string conversationId,
        Uri serviceUrl,
        string teamId,
        string tenantId)
    {
        var integration = await integrationRepository.GetByTeamsConfigurationTenantIdTeamId(
            tenantId: tenantId,
            teamId: teamId);

        var teamsConfig = TeamsIntegration.FromConfiguration(integration?.Configuration);
        if (integration is null || teamsConfig is null || teamsConfig.IsCompleted)
        {
            return;
        }

        integration.Configuration = JsonSerializer.Serialize(teamsConfig with
        {
            ChannelId = conversationId,
            ServiceUrl = serviceUrl,
            DisconnectedDate = null
        });

        await integrationRepository.UpsertAsync(integration);
        await InvalidateConfigurationCacheAsync(integration.OrganizationId);
    }

    internal async Task HandleAppRemovalAsync(string teamId, string tenantId)
    {
        var integration = await integrationRepository.GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(
            tenantId: tenantId,
            teamId: teamId);

        var teamsConfig = TeamsIntegration.FromConfiguration(integration?.Configuration);
        if (integration is null || teamsConfig is null || teamsConfig.NeedsReconnection)
        {
            logger.LogInformation(
                "Teams app removal received with no matching connected integration; nothing to disconnect");
            return;
        }

        // The tenant and team list are kept so re-installing the app reconnects without a new OAuth flow.
        integration.Configuration = JsonSerializer.Serialize(teamsConfig with
        {
            ChannelId = null,
            ServiceUrl = null,
            DisconnectedDate = timeProvider.GetUtcNow().UtcDateTime
        });

        await integrationRepository.UpsertAsync(integration);
        await InvalidateConfigurationCacheAsync(integration.OrganizationId);

        logger.LogInformation(
            "Teams integration {IntegrationId} marked as needing reconnection after app removal",
            integration.Id);
    }

    private async Task DisconnectFromActivityAsync(IActivity activity)
    {
        var teamId = activity.TeamsGetTeamInfo()?.AadGroupId;
        var tenantId = activity.Conversation?.TenantId;

        if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(tenantId))
        {
            logger.LogWarning("Teams removal activity received without a team or tenant id; ignoring");
            return;
        }

        await HandleAppRemovalAsync(teamId: teamId, tenantId: tenantId);
    }

    /// <summary>
    /// Drops the cached configuration details so a connect or disconnect takes effect on the event dispatch
    /// path immediately, rather than after the entry's one-day TTL.
    /// </summary>
    private async Task InvalidateConfigurationCacheAsync(Guid organizationId)
    {
        await cache.RemoveByTagAsync(
            EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId: organizationId,
                integrationType: IntegrationType.Teams));
    }
}

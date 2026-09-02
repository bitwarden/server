#nullable enable

using System.Net;
using System.Text.Json;
using System.Web;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Models.Data.Teams;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services.Implementations;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.MockedHttpClient;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using BotTeamInfo = Microsoft.Bot.Schema.Teams.TeamInfo;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;
using TeamsChannelData = Microsoft.Bot.Schema.Teams.TeamsChannelData;

namespace Bit.Core.Test.Dirt.Services;

[SutProviderCustomize]
public class TeamsServiceTests
{
    private readonly MockedHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;

    public TeamsServiceTests()
    {
        _handler = new MockedHttpMessageHandler();
        _httpClient = _handler.ToHttpClient();
    }

    private static readonly DateTimeOffset _now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    private SutProvider<TeamsService> GetSutProvider()
    {
        var clientFactory = Substitute.For<IHttpClientFactory>();
        clientFactory.CreateClient(TeamsService.HttpClientName).Returns(_httpClient);

        var globalSettings = Substitute.For<GlobalSettings>();
        globalSettings.Teams.LoginBaseUrl.Returns("https://login.example.com");
        globalSettings.Teams.GraphBaseUrl.Returns("https://graph.example.com");

        return new SutProvider<TeamsService>()
            .SetDependency(clientFactory)
            .SetDependency(globalSettings)
            .SetDependency<TimeProvider>(new FakeTimeProvider(_now))
            .Create();
    }

    [Fact]
    public void GetRedirectUrl_ReturnsCorrectUrl()
    {
        var sutProvider = GetSutProvider();
        var clientId = sutProvider.GetDependency<GlobalSettings>().Teams.ClientId;
        var scopes = sutProvider.GetDependency<GlobalSettings>().Teams.Scopes;
        var callbackUrl = "https://example.com/callback";
        var state = Guid.NewGuid().ToString();
        var result = sutProvider.Sut.GetRedirectUrl(callbackUrl, state);

        var uri = new Uri(result);
        var query = HttpUtility.ParseQueryString(uri.Query);

        Assert.Equal(clientId, query["client_id"]);
        Assert.Equal(scopes, query["scope"]);
        Assert.Equal(callbackUrl, query["redirect_uri"]);
        Assert.Equal(state, query["state"]);
        Assert.Equal("login.example.com", uri.Host);
        Assert.Equal("/common/oauth2/v2.0/authorize", uri.AbsolutePath);
    }

    [Fact]
    public async Task ObtainTokenViaOAuth_Success_ReturnsAccessToken()
    {
        var sutProvider = GetSutProvider();
        var jsonResponse = JsonSerializer.Serialize(new
        {
            access_token = "test-access-token"
        });

        _handler.When("https://login.example.com/common/oauth2/v2.0/token")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(jsonResponse));

        var result = await sutProvider.Sut.ObtainTokenViaOAuth("test-code", "https://example.com/callback");

        Assert.Equal("test-access-token", result);
    }

    [Theory]
    [InlineData("test-code", "")]
    [InlineData("", "https://example.com/callback")]
    [InlineData("", "")]
    public async Task ObtainTokenViaOAuth_CodeOrRedirectUrlIsEmpty_ReturnsEmptyString(string code, string redirectUrl)
    {
        var sutProvider = GetSutProvider();
        var result = await sutProvider.Sut.ObtainTokenViaOAuth(code, redirectUrl);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ObtainTokenViaOAuth_HttpFailure_ReturnsEmptyString()
    {
        var sutProvider = GetSutProvider();
        _handler.When("https://login.example.com/common/oauth2/v2.0/token")
            .RespondWith(HttpStatusCode.InternalServerError)
            .WithContent(new StringContent(string.Empty));

        var result = await sutProvider.Sut.ObtainTokenViaOAuth("test-code", "https://example.com/callback");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ObtainTokenViaOAuth_UnknownResponse_ReturnsEmptyString()
    {
        var sutProvider = GetSutProvider();

        _handler.When("https://login.example.com/common/oauth2/v2.0/token")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent("Not an expected response"));

        var result = await sutProvider.Sut.ObtainTokenViaOAuth("test-code", "https://example.com/callback");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetJoinedTeamsAsync_Success_ReturnsTeams()
    {
        var sutProvider = GetSutProvider();

        var jsonResponse = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { id = "team1", displayName = "Team One" },
                new { id = "team2", displayName = "Team Two" }
            }
        });

        _handler.When("https://graph.example.com/me/joinedTeams")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(jsonResponse));

        var result = await sutProvider.Sut.GetJoinedTeamsAsync("fake-access-token");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t is { Id: "team1", DisplayName: "Team One" });
        Assert.Contains(result, t => t is { Id: "team2", DisplayName: "Team Two" });
    }

    [Fact]
    public async Task GetJoinedTeamsAsync_ServerReturnsEmpty_ReturnsEmptyList()
    {
        var sutProvider = GetSutProvider();

        var jsonResponse = JsonSerializer.Serialize(new { value = (object?)null });

        _handler.When("https://graph.example.com/me/joinedTeams")
            .RespondWith(HttpStatusCode.OK)
            .WithContent(new StringContent(jsonResponse));

        var result = await sutProvider.Sut.GetJoinedTeamsAsync("fake-access-token");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetJoinedTeamsAsync_ServerErrorCode_ReturnsEmptyList()
    {
        var sutProvider = GetSutProvider();

        _handler.When("https://graph.example.com/me/joinedTeams")
            .RespondWith(HttpStatusCode.Unauthorized)
            .WithContent(new StringContent("Unauthorized"));

        var result = await sutProvider.Sut.GetJoinedTeamsAsync("fake-access-token");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task HandleIncomingAppInstall_Success_UpdatesTeamsIntegration(
        OrganizationIntegration integration)
    {
        var sutProvider = GetSutProvider();
        var tenantId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid().ToString();
        var conversationId = Guid.NewGuid().ToString();
        var serviceUrl = new Uri("https://localhost");
        var initiatedConfiguration = new TeamsIntegration(TenantId: tenantId, Teams:
        [
            new TeamInfo() { Id = teamId, DisplayName = "test team", TenantId = tenantId },
            new TeamInfo() { Id = Guid.NewGuid().ToString(), DisplayName = "other team", TenantId = tenantId },
            new TeamInfo() { Id = Guid.NewGuid().ToString(), DisplayName = "third team", TenantId = tenantId }
        ]);
        integration.Configuration = JsonSerializer.Serialize(initiatedConfiguration);

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByTeamsConfigurationTenantIdTeamId(tenantId, teamId)
            .Returns(integration);

        OrganizationIntegration? capturedIntegration = null;
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .UpsertAsync(Arg.Do<OrganizationIntegration>(x => capturedIntegration = x));

        await sutProvider.Sut.HandleIncomingAppInstallAsync(
            conversationId: conversationId,
            serviceUrl: serviceUrl,
            teamId: teamId,
            tenantId: tenantId
        );

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1).GetByTeamsConfigurationTenantIdTeamId(tenantId, teamId);
        Assert.NotNull(capturedIntegration);
        var configuration = JsonSerializer.Deserialize<TeamsIntegration>(capturedIntegration.Configuration ?? string.Empty);
        Assert.NotNull(configuration);
        Assert.NotNull(configuration.ServiceUrl);
        Assert.Equal(serviceUrl, configuration.ServiceUrl);
        Assert.Equal(conversationId, configuration.ChannelId);
    }

    [Theory, BitAutoData]
    public async Task HandleIncomingAppInstall_PreviouslyDisconnected_ReconnectsAndClearsDisconnectedDate(
        OrganizationIntegration integration)
    {
        var sutProvider = GetSutProvider();
        var tenantId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid().ToString();
        var serviceUrl = new Uri("https://localhost");
        var disconnectedConfiguration = new TeamsIntegration(
            TenantId: tenantId,
            Teams: [new TeamInfo() { Id = teamId, DisplayName = "test team", TenantId = tenantId }],
            DisconnectedDate: _now.UtcDateTime.AddDays(-3)
        );
        integration.Configuration = JsonSerializer.Serialize(disconnectedConfiguration);

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByTeamsConfigurationTenantIdTeamId(tenantId, teamId)
            .Returns(integration);

        OrganizationIntegration? capturedIntegration = null;
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .UpsertAsync(Arg.Do<OrganizationIntegration>(x => capturedIntegration = x));

        await sutProvider.Sut.HandleIncomingAppInstallAsync(
            conversationId: "new-conversation",
            serviceUrl: serviceUrl,
            teamId: teamId,
            tenantId: tenantId
        );

        Assert.NotNull(capturedIntegration);
        var configuration = TeamsIntegration.FromConfiguration(capturedIntegration.Configuration);
        Assert.NotNull(configuration);
        Assert.Null(configuration.DisconnectedDate);
        Assert.False(configuration.NeedsReconnection);
        Assert.True(configuration.IsCompleted);
        Assert.Equal("new-conversation", configuration.ChannelId);
        Assert.Equal(serviceUrl, configuration.ServiceUrl);
    }

    [Theory, BitAutoData]
    public async Task HandleAppRemoval_Success_ClearsChannelAndMarksDisconnected(
        OrganizationIntegration integration)
    {
        var sutProvider = GetSutProvider();
        var tenantId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid().ToString();
        var connectedConfiguration = new TeamsIntegration(
            TenantId: tenantId,
            Teams: [new TeamInfo() { Id = teamId, DisplayName = "test team", TenantId = tenantId }],
            ChannelId: "channel-id",
            ServiceUrl: new Uri("https://smba.example.com")
        );
        integration.Configuration = JsonSerializer.Serialize(connectedConfiguration);

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(tenantId, teamId)
            .Returns(integration);

        OrganizationIntegration? capturedIntegration = null;
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .UpsertAsync(Arg.Do<OrganizationIntegration>(x => capturedIntegration = x));

        await sutProvider.Sut.HandleAppRemovalAsync(teamId: teamId, tenantId: tenantId);

        Assert.NotNull(capturedIntegration);
        var configuration = TeamsIntegration.FromConfiguration(capturedIntegration.Configuration);
        Assert.NotNull(configuration);
        Assert.Null(configuration.ChannelId);
        Assert.Null(configuration.ServiceUrl);
        Assert.Equal(_now.UtcDateTime, configuration.DisconnectedDate);
        Assert.True(configuration.NeedsReconnection);
        Assert.False(configuration.IsCompleted);

        // Tenant and team list are retained so a re-install can reconnect without a new OAuth flow.
        Assert.Equal(tenantId, configuration.TenantId);
        Assert.Equal(teamId, Assert.Single(configuration.Teams).Id);

        await sutProvider.GetDependency<IFusionCache>().Received(1).RemoveByTagAsync(
            EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                integration.OrganizationId,
                IntegrationType.Teams),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAppRemoval_NoConnectedIntegrationMatched_DoesNothing()
    {
        var sutProvider = GetSutProvider();

        await sutProvider.Sut.HandleAppRemovalAsync(teamId: "teamId", tenantId: "tenantId");

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .GetConnectedByTeamsConfigurationTenantIdTeamIdAsync("tenantId", "teamId");
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive()
            .UpsertAsync(Arg.Any<OrganizationIntegration>());
    }

    [Theory, BitAutoData]
    public async Task HandleAppRemoval_AlreadyDisconnected_DoesNothing(OrganizationIntegration integration)
    {
        var sutProvider = GetSutProvider();
        var tenantId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid().ToString();
        integration.Configuration = JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: tenantId,
            Teams: [new TeamInfo() { Id = teamId, DisplayName = "test team", TenantId = tenantId }],
            DisconnectedDate: _now.UtcDateTime.AddDays(-1)
        ));

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(tenantId, teamId)
            .Returns(integration);

        await sutProvider.Sut.HandleAppRemovalAsync(teamId: teamId, tenantId: tenantId);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive()
            .UpsertAsync(Arg.Any<OrganizationIntegration>());
    }

    [Theory, BitAutoData]
    public async Task HandleAppRemoval_MalformedConfiguration_DoesNothing(OrganizationIntegration integration)
    {
        var sutProvider = GetSutProvider();
        integration.Configuration = "{not-valid-json";

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetConnectedByTeamsConfigurationTenantIdTeamIdAsync("tenantId", "teamId")
            .Returns(integration);

        await sutProvider.Sut.HandleAppRemovalAsync(teamId: "teamId", tenantId: "tenantId");

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive()
            .UpsertAsync(Arg.Any<OrganizationIntegration>());
    }

    [Theory, BitAutoData]
    public async Task OnTurn_InstallationUpdateRemove_DisconnectsIntegration(OrganizationIntegration integration)
    {
        var sutProvider = GetSutProvider();
        var tenantId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid().ToString();
        ArrangeConnectedIntegration(sutProvider, integration, tenantId, teamId);

        var activity = BuildTeamsActivity(ActivityTypes.InstallationUpdate, tenantId, teamId);
        activity.Action = "remove";

        await sutProvider.Sut.OnTurnAsync(new TurnContext(new TestAdapter(), activity), CancellationToken.None);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .UpsertAsync(Arg.Any<OrganizationIntegration>());
    }

    [Theory, BitAutoData]
    public async Task OnTurn_InstallationUpdateAdd_DoesNotDisconnect(OrganizationIntegration integration)
    {
        var sutProvider = GetSutProvider();
        var tenantId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid().ToString();
        ArrangeConnectedIntegration(sutProvider, integration, tenantId, teamId);

        var activity = BuildTeamsActivity(ActivityTypes.InstallationUpdate, tenantId, teamId);
        activity.Action = "add";

        await sutProvider.Sut.OnTurnAsync(new TurnContext(new TestAdapter(), activity), CancellationToken.None);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive()
            .GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task OnTurn_ConversationUpdateWithBotRemoved_DisconnectsIntegration(
        OrganizationIntegration integration)
    {
        var sutProvider = GetSutProvider();
        var tenantId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid().ToString();
        ArrangeConnectedIntegration(sutProvider, integration, tenantId, teamId);

        var activity = BuildTeamsActivity(ActivityTypes.ConversationUpdate, tenantId, teamId);
        activity.MembersRemoved = [new ChannelAccount { Id = _botId }];

        await sutProvider.Sut.OnTurnAsync(new TurnContext(new TestAdapter(), activity), CancellationToken.None);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1)
            .UpsertAsync(Arg.Any<OrganizationIntegration>());
    }

    [Theory, BitAutoData]
    public async Task OnTurn_ConversationUpdateWithOnlyOtherMembersRemoved_DoesNotDisconnect(
        OrganizationIntegration integration)
    {
        var sutProvider = GetSutProvider();
        var tenantId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid().ToString();
        ArrangeConnectedIntegration(sutProvider, integration, tenantId, teamId);

        var activity = BuildTeamsActivity(ActivityTypes.ConversationUpdate, tenantId, teamId);
        activity.MembersRemoved = [new ChannelAccount { Id = "some-departing-user" }];

        await sutProvider.Sut.OnTurnAsync(new TurnContext(new TestAdapter(), activity), CancellationToken.None);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive()
            .GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task OnTurn_ConversationUpdateMissingTeamInfo_DoesNotDisconnect(
        OrganizationIntegration integration)
    {
        var sutProvider = GetSutProvider();
        var tenantId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid().ToString();
        ArrangeConnectedIntegration(sutProvider, integration, tenantId, teamId);

        var activity = BuildTeamsActivity(ActivityTypes.ConversationUpdate, tenantId, teamId);
        activity.ChannelData = null;
        activity.MembersRemoved = [new ChannelAccount { Id = _botId }];

        await sutProvider.Sut.OnTurnAsync(new TurnContext(new TestAdapter(), activity), CancellationToken.None);

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive()
            .GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    private const string _botId = "28:bitwarden-bot";

    private static Activity BuildTeamsActivity(string type, string tenantId, string teamId) =>
        new()
        {
            Type = type,
            ChannelId = "msteams",
            ServiceUrl = "https://smba.example.com",
            Recipient = new ChannelAccount { Id = _botId },
            From = new ChannelAccount { Id = "29:some-user" },
            Conversation = new ConversationAccount { Id = "channel-id", TenantId = tenantId },
            ChannelData = new TeamsChannelData { Team = new BotTeamInfo { Id = teamId, AadGroupId = teamId } }
        };

    private static void ArrangeConnectedIntegration(
        SutProvider<TeamsService> sutProvider,
        OrganizationIntegration integration,
        string tenantId,
        string teamId)
    {
        integration.Configuration = JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: tenantId,
            Teams: [new TeamInfo() { Id = teamId, DisplayName = "test team", TenantId = tenantId }],
            ChannelId: "channel-id",
            ServiceUrl: new Uri("https://smba.example.com")
        ));

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(tenantId, teamId)
            .Returns(integration);
    }

    [Fact]
    public async Task HandleIncomingAppInstall_NoIntegrationMatched_DoesNothing()
    {
        var sutProvider = GetSutProvider();
        await sutProvider.Sut.HandleIncomingAppInstallAsync(
            conversationId: "conversationId",
            serviceUrl: new Uri("https://localhost"),
            teamId: "teamId",
            tenantId: "tenantId"
        );

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1).GetByTeamsConfigurationTenantIdTeamId("tenantId", "teamId");
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive().UpsertAsync(Arg.Any<OrganizationIntegration>());
    }

    [Theory, BitAutoData]
    public async Task HandleIncomingAppInstall_MatchedIntegrationAlreadySetup_DoesNothing(
        OrganizationIntegration integration)
    {
        var sutProvider = GetSutProvider();
        var tenantId = Guid.NewGuid().ToString();
        var teamId = Guid.NewGuid().ToString();
        var initiatedConfiguration = new TeamsIntegration(
            TenantId: tenantId,
            Teams: [new TeamInfo() { Id = teamId, DisplayName = "test team", TenantId = tenantId }],
            ChannelId: "ChannelId",
            ServiceUrl: new Uri("https://localhost")
        );
        integration.Configuration = JsonSerializer.Serialize(initiatedConfiguration);

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByTeamsConfigurationTenantIdTeamId(tenantId, teamId)
            .Returns(integration);

        await sutProvider.Sut.HandleIncomingAppInstallAsync(
            conversationId: "conversationId",
            serviceUrl: new Uri("https://localhost"),
            teamId: teamId,
            tenantId: tenantId
        );

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1).GetByTeamsConfigurationTenantIdTeamId(tenantId, teamId);
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive().UpsertAsync(Arg.Any<OrganizationIntegration>());
    }

    [Theory, BitAutoData]
    public async Task HandleIncomingAppInstall_MatchedIntegrationWithMissingConfiguration_DoesNothing(
        OrganizationIntegration integration)
    {
        var sutProvider = GetSutProvider();
        integration.Configuration = null;

        sutProvider.GetDependency<IOrganizationIntegrationRepository>()
            .GetByTeamsConfigurationTenantIdTeamId("tenantId", "teamId")
            .Returns(integration);

        await sutProvider.Sut.HandleIncomingAppInstallAsync(
            conversationId: "conversationId",
            serviceUrl: new Uri("https://localhost"),
            teamId: "teamId",
            tenantId: "tenantId"
        );

        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().Received(1).GetByTeamsConfigurationTenantIdTeamId("tenantId", "teamId");
        await sutProvider.GetDependency<IOrganizationIntegrationRepository>().DidNotReceive().UpsertAsync(Arg.Any<OrganizationIntegration>());
    }
}

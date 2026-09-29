using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Models.Data.Teams;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Dirt.Repositories;

/// <summary>
/// Covers the two Teams lookups that search inside the JSON <c>Configuration</c> column. Both have a Dapper
/// (stored procedure) and an EF Core implementation whose filters have to agree, so these run under
/// <c>[DatabaseData]</c> against every configured provider to keep the two tracks honest.
/// </summary>
public class OrganizationIntegrationRepositoryTests
{
    // Unique per test (xUnit creates a new instance for each) so rows left behind by other tests never match.
    private readonly string _tenantId = Guid.NewGuid().ToString();
    private readonly string _teamId = Guid.NewGuid().ToString();

    [Theory, DatabaseData]
    public async Task GetByTeamsConfigurationTenantIdTeamId_AwaitingInstall_ReturnsIntegration(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        await CreateTeamsIntegrationAsync(sut, organizationRepository, AwaitingInstallConfiguration());

        var result = await sut.GetByTeamsConfigurationTenantIdTeamId(_tenantId, _teamId);

        Assert.NotNull(result);
    }

    [Theory, DatabaseData]
    public async Task GetByTeamsConfigurationTenantIdTeamId_Connected_ReturnsNull(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        await CreateTeamsIntegrationAsync(sut, organizationRepository, ConnectedConfiguration());

        // A connected integration must not be reachable by the install callback, otherwise an incoming bot event
        // could re-point a working integration at another channel.
        var result = await sut.GetByTeamsConfigurationTenantIdTeamId(_tenantId, _teamId);

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task GetByTeamsConfigurationTenantIdTeamId_Disconnected_ReturnsIntegration(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        await CreateTeamsIntegrationAsync(sut, organizationRepository, DisconnectedConfiguration());

        // Disconnected integrations stay eligible so re-installing the app reconnects without a new OAuth flow.
        var result = await sut.GetByTeamsConfigurationTenantIdTeamId(_tenantId, _teamId);

        Assert.NotNull(result);
    }

    [Theory, DatabaseData]
    public async Task GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync_Connected_ReturnsIntegration(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var integration = await CreateTeamsIntegrationAsync(
            sut,
            organizationRepository,
            ConnectedConfiguration());

        var result = await sut.GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync(_tenantId, _teamId);

        Assert.Equal(integration.Id, Assert.Single(result).Id);
    }

    [Theory, DatabaseData]
    public async Task GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync_AwaitingInstall_ReturnsEmpty(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        await CreateTeamsIntegrationAsync(sut, organizationRepository, AwaitingInstallConfiguration());

        var result = await sut.GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync(_tenantId, _teamId);

        Assert.Empty(result);
    }

    [Theory, DatabaseData]
    public async Task GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync_Disconnected_ReturnsEmpty(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        await CreateTeamsIntegrationAsync(sut, organizationRepository, DisconnectedConfiguration());

        // Already disconnected, so a second removal event has nothing to tear down.
        var result = await sut.GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync(_tenantId, _teamId);

        Assert.Empty(result);
    }

    [Theory, DatabaseData]
    public async Task GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync_DifferentTeam_ReturnsEmpty(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        await CreateTeamsIntegrationAsync(sut, organizationRepository, ConnectedConfiguration());

        var result = await sut.GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync(
            _tenantId,
            "a-different-team-id");

        Assert.Empty(result);
    }

    [Theory, DatabaseData]
    public async Task GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync_MatchesTeamOtherThanFirst_ReturnsIntegration(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var configuration = JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: _tenantId,
            Teams:
            [
                new TeamInfo { Id = "another-team", DisplayName = "Another Team", TenantId = _tenantId },
                new TeamInfo { Id = _teamId, DisplayName = "Test Team", TenantId = _tenantId }
            ],
            ChannelId: "channel-id",
            ServiceUrl: new Uri("https://smba.example.com")
        ));
        var integration = await CreateTeamsIntegrationAsync(sut, organizationRepository, configuration);

        // The team list holds every team the owner belongs to, so the match cannot assume the first entry.
        var result = await sut.GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync(_tenantId, _teamId);

        Assert.Equal(integration.Id, Assert.Single(result).Id);
    }

    [Theory, DatabaseData]
    public async Task GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync_TeamListedTwice_ReturnsIntegrationOnce(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var configuration = JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: _tenantId,
            Teams:
            [
                new TeamInfo { Id = _teamId, DisplayName = "Test Team", TenantId = _tenantId },
                new TeamInfo { Id = _teamId, DisplayName = "Test Team", TenantId = _tenantId }
            ],
            ChannelId: "channel-id",
            ServiceUrl: new Uri("https://smba.example.com")
        ));
        var integration = await CreateTeamsIntegrationAsync(sut, organizationRepository, configuration);

        // The team match must not fan out into one row per matching entry in the team list.
        var result = await sut.GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync(_tenantId, _teamId);

        Assert.Equal(integration.Id, Assert.Single(result).Id);
    }

    [Theory, DatabaseData]
    public async Task GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync_TwoOrganizationsSameTeam_ReturnsBoth(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var first = await CreateTeamsIntegrationAsync(sut, organizationRepository, ConnectedConfiguration());
        var second = await CreateTeamsIntegrationAsync(sut, organizationRepository, ConnectedConfiguration());

        // Nothing stops two organizations connecting the same Teams team, and removing the app from that team
        // disconnects both, so every match must be returned.
        var result = await sut.GetManyConnectedByTeamsConfigurationTenantIdTeamIdAsync(_tenantId, _teamId);

        Assert.Equal(
            new[] { first.Id, second.Id }.Order(),
            result.Select(integration => integration.Id).Order());
    }

    private string AwaitingInstallConfiguration() =>
        JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: _tenantId,
            Teams: [new TeamInfo { Id = _teamId, DisplayName = "Test Team", TenantId = _tenantId }]
        ));

    private string ConnectedConfiguration() =>
        JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: _tenantId,
            Teams: [new TeamInfo { Id = _teamId, DisplayName = "Test Team", TenantId = _tenantId }],
            ChannelId: "channel-id",
            ServiceUrl: new Uri("https://smba.example.com")
        ));

    private string DisconnectedConfiguration() =>
        JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: _tenantId,
            Teams: [new TeamInfo { Id = _teamId, DisplayName = "Test Team", TenantId = _tenantId }],
            DisconnectedDate: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        ));

    private static async Task<OrganizationIntegration> CreateTeamsIntegrationAsync(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository,
        string configuration)
    {
        // A unique index on (OrganizationId, Type) allows only one Teams integration per organization.
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {Guid.NewGuid()}",
            BillingEmail = "test@email.com",
            Plan = "Test",
            PrivateKey = "privatekey"
        });

        return await sut.CreateAsync(new OrganizationIntegration
        {
            OrganizationId = organization.Id,
            Type = IntegrationType.Teams,
            Configuration = configuration
        });
    }
}

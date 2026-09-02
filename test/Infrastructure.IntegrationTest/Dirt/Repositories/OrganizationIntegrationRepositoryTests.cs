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
    private const string _tenantId = "11111111-2222-3333-4444-555555555555";
    private const string _teamId = "66666666-7777-8888-9999-000000000000";

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
    public async Task GetConnectedByTeamsConfigurationTenantIdTeamIdAsync_Connected_ReturnsIntegration(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        var integration = await CreateTeamsIntegrationAsync(
            sut,
            organizationRepository,
            ConnectedConfiguration());

        var result = await sut.GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(_tenantId, _teamId);

        Assert.NotNull(result);
        Assert.Equal(integration.Id, result.Id);
    }

    [Theory, DatabaseData]
    public async Task GetConnectedByTeamsConfigurationTenantIdTeamIdAsync_AwaitingInstall_ReturnsNull(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        await CreateTeamsIntegrationAsync(sut, organizationRepository, AwaitingInstallConfiguration());

        var result = await sut.GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(_tenantId, _teamId);

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task GetConnectedByTeamsConfigurationTenantIdTeamIdAsync_Disconnected_ReturnsNull(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        await CreateTeamsIntegrationAsync(sut, organizationRepository, DisconnectedConfiguration());

        // Already disconnected, so a second removal event has nothing to tear down.
        var result = await sut.GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(_tenantId, _teamId);

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task GetConnectedByTeamsConfigurationTenantIdTeamIdAsync_DifferentTeam_ReturnsNull(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        await CreateTeamsIntegrationAsync(sut, organizationRepository, ConnectedConfiguration());

        var result = await sut.GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(
            _tenantId,
            "a-different-team-id");

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task GetConnectedByTeamsConfigurationTenantIdTeamIdAsync_MatchesTeamOtherThanFirst_ReturnsIntegration(
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
        await CreateTeamsIntegrationAsync(sut, organizationRepository, configuration);

        // The team list holds every team the owner belongs to, so the match cannot assume the first entry.
        var result = await sut.GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(_tenantId, _teamId);

        Assert.NotNull(result);
    }

    [Theory, DatabaseData]
    public async Task GetConnectedByTeamsConfigurationTenantIdTeamIdAsync_TwoOrganizationsSameTeam_ReturnsOne(
        IOrganizationIntegrationRepository sut,
        IOrganizationRepository organizationRepository)
    {
        await CreateTeamsIntegrationAsync(sut, organizationRepository, ConnectedConfiguration());
        await CreateTeamsIntegrationAsync(sut, organizationRepository, ConnectedConfiguration());

        // Nothing stops two organizations connecting the same Teams team. The result is arbitrary, but it must
        // not throw — which is why both implementations take the first match rather than requiring a single one.
        var result = await sut.GetConnectedByTeamsConfigurationTenantIdTeamIdAsync(_tenantId, _teamId);

        Assert.NotNull(result);
    }

    private static string AwaitingInstallConfiguration() =>
        JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: _tenantId,
            Teams: [new TeamInfo { Id = _teamId, DisplayName = "Test Team", TenantId = _tenantId }]
        ));

    private static string ConnectedConfiguration() =>
        JsonSerializer.Serialize(new TeamsIntegration(
            TenantId: _tenantId,
            Teams: [new TeamInfo { Id = _teamId, DisplayName = "Test Team", TenantId = _tenantId }],
            ChannelId: "channel-id",
            ServiceUrl: new Uri("https://smba.example.com")
        ));

    private static string DisconnectedConfiguration() =>
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

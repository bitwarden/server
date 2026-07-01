using System.Text.Json;
using Bit.Core.Services;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

/// <summary>
/// Verifies the play-id cleanup path for <see cref="MigrationCohortExportScene"/>. The scene inserts
/// its organizations via BulkCopy, which bypasses the repository tracking decorators, so the scene
/// records the <c>PlayItem</c> rows itself. These tests confirm that <c>DELETE /seed/{playId}</c> can
/// therefore tear the bulk-seeded organizations down.
/// </summary>
/// <remarks>
/// Uses a dedicated factory that enables a real (fixed) <see cref="IPlayIdService"/> -- the shared
/// <see cref="SeederApiApplicationFactory"/> registers <c>NeverPlayIdServices</c>, under which the
/// scene's play-id recording is a deliberate no-op.
/// </remarks>
public class MigrationCohortExportSceneCleanupTests
    : IClassFixture<PlayIdEnabledSeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly PlayIdEnabledSeederApiApplicationFactory _factory;
    private const string _username = "username";
    private const string _password = "pass";

    public MigrationCohortExportSceneCleanupTests(PlayIdEnabledSeederApiApplicationFactory factory)
    {
        _factory = factory;
        factory.ConfigureAuth(_username, _password);
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(_username, _password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Seed_WithPlayId_RecordsOnePlayItemPerOrganizationLinkingThemForCleanup()
    {
        const int orgCount = 4;
        var cohortName = $"PM-36965 Cleanup {Guid.NewGuid()}";
        var namePrefix = $"pm36965-{Guid.NewGuid():N}-";
        var playId = _factory.PlayId;

        var seedResponse = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "MigrationCohortExportScene",
            Arguments = JsonSerializer.SerializeToElement(new MigrationCohortExportScene.Request
            {
                CohortName = cohortName,
                OrgCount = orgCount,
                NamePrefix = namePrefix
            })
        }, playId);
        seedResponse.EnsureSuccessStatusCode();

        var db = _factory.GetDatabaseContext();

        var orgIds = await db.Organizations
            .Where(o => o.Name.StartsWith(namePrefix))
            .Select(o => o.Id)
            .ToListAsync();
        Assert.Equal(orgCount, orgIds.Count);

        // The scene bulk-inserts a PlayItem per organization (BulkCopy bypasses the repository
        // tracking decorator), which is exactly what lets DELETE /seed/{playId} find and remove them.
        var trackedOrgIds = await db.PlayItem
            .Where(p => p.PlayId == playId && p.OrganizationId != null)
            .Select(p => p.OrganizationId!.Value)
            .ToListAsync();

        Assert.Equal(orgCount, trackedOrgIds.Count);
        Assert.Equal(orgIds.ToHashSet(), trackedOrgIds.ToHashSet());
    }
}

/// <summary>
/// Factory variant that registers a fixed <see cref="IPlayIdService"/> so play-id tracking is active
/// for the cleanup tests (the base factory registers <c>NeverPlayIdServices</c>).
/// </summary>
public class PlayIdEnabledSeederApiApplicationFactory : SeederApiApplicationFactory
{
    public string PlayId { get; } = $"cleanup-{Guid.NewGuid()}";

    public PlayIdEnabledSeederApiApplicationFactory()
    {
        _configureTestServices.Add(services =>
        {
            services.RemoveAll<IPlayIdService>();
            services.AddSingleton<IPlayIdService>(new FixedPlayIdService(PlayId));
        });
    }

    private sealed class FixedPlayIdService(string playId) : IPlayIdService
    {
        public string? PlayId { get; set; } = playId;

        public bool InPlay(out string playId)
        {
            playId = PlayId ?? string.Empty;
            return !string.IsNullOrEmpty(playId);
        }
    }
}

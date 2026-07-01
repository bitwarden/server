using System.Net;
using System.Text.Json;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class SeedControllerTests : IClassFixture<SeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly SeederApiApplicationFactory _factory;
    private readonly string Username = "username";
    private readonly string Password = "pass";


    public SeedControllerTests(SeederApiApplicationFactory factory)
    {
        _factory = factory;
        factory.ConfigureAuth(Username, Password);
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(Username, Password);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Clean up any seeded data after each test
        await _client.DeleteAsync("/seed");
        _client.Dispose();
    }

    [Fact]
    public async Task SeedEndpoint_WithValidScene_ReturnsOk()
    {
        var testEmail = $"seed-test-{Guid.NewGuid()}@bitwarden.com";
        var playId = Guid.NewGuid().ToString();

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleUserScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new SingleUserScene.Request() { Email = testEmail, Password = "asdfasdfasdf" })
        }, playId);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SceneResponseModel>();

        Assert.NotNull(result);
        Assert.NotNull(result.MangleMap);
        Assert.NotNull(result.Result);
    }

    [Fact]
    public async Task SeedEndpoint_WithInvalidSceneName_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "NonExistentScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new SingleUserScene.Request() { Email = "test@example.com", Password = "asdfasdfasdf" })
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SeedEndpoint_WithMissingRequiredField_ReturnsBadRequest()
    {
        // SingleUserScene requires 'email' field
        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleUserScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { wrongField = "value" })
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEndpoint_WithValidPlayId_ReturnsOk()
    {
        var testEmail = $"delete-test-{Guid.NewGuid()}@bitwarden.com";
        var playId = Guid.NewGuid().ToString();

        var seedResponse = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleUserScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new SingleUserScene.Request() { Email = testEmail, Password = "asdfasdfasdf" })
        }, playId);

        seedResponse.EnsureSuccessStatusCode();
        var seedResult = await seedResponse.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(seedResult);

        var deleteResponse = await _client.DeleteAsync($"/seed/{playId}");
        deleteResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DeleteEndpoint_WithInvalidPlayId_ReturnsOk()
    {
        // DestroyRecipe is idempotent - returns null for non-existent play IDs
        var nonExistentPlayId = Guid.NewGuid().ToString();
        var response = await _client.DeleteAsync($"/seed/{nonExistentPlayId}");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal($$"""{"playId":"{{nonExistentPlayId}}"}""", content);
    }

    [Fact]
    public async Task DeleteBatchEndpoint_WithValidPlayIds_ReturnsOk()
    {
        // Create multiple seeds with different play IDs
        var playIds = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var playId = Guid.NewGuid().ToString();
            playIds.Add(playId);

            var testEmail = $"batch-test-{Guid.NewGuid()}@bitwarden.com";
            var seedResponse = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
            {
                Template = "SingleUserScene",
                Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new SingleUserScene.Request() { Email = testEmail, Password = "asdfasdfasdf" })
            }, playId);

            seedResponse.EnsureSuccessStatusCode();
            var seedResult = await seedResponse.Content.ReadFromJsonAsync<SceneResponseModel>();
            Assert.NotNull(seedResult);
        }

        // Delete them in batch
        var request = new HttpRequestMessage(HttpMethod.Delete, "/seed/batch")
        {
            Content = JsonContent.Create(playIds)
        };
        var deleteResponse = await _client.SendAsync(request);
        deleteResponse.EnsureSuccessStatusCode();

        var result = await deleteResponse.Content.ReadFromJsonAsync<BatchDeleteResponse>();
        Assert.NotNull(result);
        Assert.Equal("Batch delete completed successfully", result.Message);
    }

    [Fact]
    public async Task DeleteBatchEndpoint_WithSomeInvalidIds_ReturnsOk()
    {
        // DestroyRecipe is idempotent - batch delete succeeds even with non-existent IDs
        // Create one valid seed with a play ID
        var validPlayId = Guid.NewGuid().ToString();
        var testEmail = $"batch-partial-test-{Guid.NewGuid()}@bitwarden.com";

        var seedResponse = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleUserScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new SingleUserScene.Request() { Email = testEmail, Password = "asdfasdfasdf" })
        }, validPlayId);

        seedResponse.EnsureSuccessStatusCode();
        var seedResult = await seedResponse.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(seedResult);

        // Try to delete with mix of valid and invalid IDs
        var playIds = new List<string> { validPlayId, Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
        var request = new HttpRequestMessage(HttpMethod.Delete, "/seed/batch")
        {
            Content = JsonContent.Create(playIds)
        };
        var deleteResponse = await _client.SendAsync(request);

        deleteResponse.EnsureSuccessStatusCode();
        var result = await deleteResponse.Content.ReadFromJsonAsync<BatchDeleteResponse>();
        Assert.NotNull(result);
        Assert.Equal("Batch delete completed successfully", result.Message);
    }

    [Fact]
    public async Task DeleteAllEndpoint_DeletesAllSeededData()
    {
        // Create multiple seeds
        for (var i = 0; i < 2; i++)
        {
            var playId = Guid.NewGuid().ToString();
            var testEmail = $"deleteall-test-{Guid.NewGuid()}@bitwarden.com";

            var seedResponse = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
            {
                Template = "SingleUserScene",
                Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new SingleUserScene.Request() { Email = testEmail, Password = "asdfasdfasdf" })
            }, playId);

            seedResponse.EnsureSuccessStatusCode();
        }

        // Delete all
        var deleteResponse = await _client.DeleteAsync("/seed");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task SeedEndpoint_VerifyResponseContainsMangleMapAndResult()
    {
        var testEmail = $"verify-response-{Guid.NewGuid()}@bitwarden.com";
        var playId = Guid.NewGuid().ToString();

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleUserScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new SingleUserScene.Request() { Email = testEmail, Password = "asdfasdfasdf" })
        }, playId);

        response.EnsureSuccessStatusCode();
        var jsonString = await response.Content.ReadAsStringAsync();

        // Verify the response contains MangleMap and Result fields
        Assert.Contains("mangleMap", jsonString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("result", jsonString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SeedEndpoint_MigrationCohortExportScene_PersistsCohortOrgsAndAssignmentsToDatabase()
    {
        const int orgCount = 5;
        var cohortName = $"PM-36965 Export Test {Guid.NewGuid()}";
        var namePrefix = $"pm36965-{Guid.NewGuid():N}-";

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "MigrationCohortExportScene",
            Arguments = JsonSerializer.SerializeToElement(new MigrationCohortExportScene.Request
            {
                CohortName = cohortName,
                OrgCount = orgCount,
                NamePrefix = namePrefix
            })
        }, Guid.NewGuid().ToString());

        response.EnsureSuccessStatusCode();

        // Read the data back straight from the database -- proving the rows actually landed,
        // not merely that the API reported success.
        var db = _factory.GetDatabaseContext();

        var cohort = await db.OrganizationPlanMigrationCohorts
            .SingleOrDefaultAsync(c => c.Name == cohortName);
        Assert.NotNull(cohort);
        Assert.True(cohort.IsActive);
        // Default request => a migration cohort on the default path.
        Assert.Equal(MigrationPathId.Enterprise2020AnnualToCurrent, cohort.MigrationPathId);

        var orgs = await db.Organizations
            .Where(o => o.Name.StartsWith(namePrefix))
            .ToListAsync();
        Assert.Equal(orgCount, orgs.Count);
        Assert.All(orgs, o => Assert.False(o.Enabled)); // seeded orgs are inert

        var assignments = await db.OrganizationPlanMigrationCohortAssignments
            .Where(a => a.CohortId == cohort.Id)
            .ToListAsync();
        Assert.Equal(orgCount, assignments.Count);

        // Every seeded org has exactly one assignment to this cohort.
        var orgIds = orgs.Select(o => o.Id).ToHashSet();
        Assert.Equal(orgIds, assignments.Select(a => a.OrganizationId).ToHashSet());

        // This factory registers NeverPlayIdServices, so play-id recording is a no-op: no PlayItem
        // rows are written for a seed that carries no active play id.
        Assert.Equal(0, await db.PlayItem.CountAsync(p => orgIds.Contains(p.OrganizationId!.Value)));
    }

    [Fact]
    public async Task SeedEndpoint_MigrationCohortExportScene_ReusesExistingCohortByName()
    {
        var cohortName = $"PM-36965 Reuse {Guid.NewGuid()}";

        async Task<JsonElement> SeedAsync() =>
            (await (await _client.PostAsJsonAsync("/seed", new SeedRequestModel
            {
                Template = "MigrationCohortExportScene",
                Arguments = JsonSerializer.SerializeToElement(
                    new MigrationCohortExportScene.Request { CohortName = cohortName, OrgCount = 2 })
            }, Guid.NewGuid().ToString()))
            .EnsureSuccessStatusCode()
            .Content.ReadFromJsonAsync<JsonElement>());

        var first = (await SeedAsync()).GetProperty("result");
        var second = (await SeedAsync()).GetProperty("result");

        // Second run finds the cohort by name and reuses it rather than creating a duplicate.
        Assert.True(first.GetProperty("cohortCreated").GetBoolean());
        Assert.False(second.GetProperty("cohortCreated").GetBoolean());
        Assert.Equal(first.GetProperty("cohortId").GetGuid(), second.GetProperty("cohortId").GetGuid());
    }

    [Fact]
    public async Task SeedEndpoint_MigrationCohortExportScene_NullMigrationPathId_CreatesChurnOnlyCohort()
    {
        var cohortName = $"PM-36965 Churn {Guid.NewGuid()}";
        var namePrefix = $"pm36965-{Guid.NewGuid():N}-";

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "MigrationCohortExportScene",
            Arguments = JsonSerializer.SerializeToElement(new MigrationCohortExportScene.Request
            {
                CohortName = cohortName,
                OrgCount = 2,
                NamePrefix = namePrefix,
                MigrationPathId = null
            })
        }, Guid.NewGuid().ToString());

        response.EnsureSuccessStatusCode();

        var db = _factory.GetDatabaseContext();
        var cohort = await db.OrganizationPlanMigrationCohorts.SingleAsync(c => c.Name == cohortName);

        // A null path persists as null: a churn-only cohort, not the default migration path.
        Assert.Null(cohort.MigrationPathId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100_001)]
    public async Task SeedEndpoint_MigrationCohortExportScene_OrgCountOutOfRange_ReturnsBadRequest(int orgCount)
    {
        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "MigrationCohortExportScene",
            Arguments = JsonSerializer.SerializeToElement(new MigrationCohortExportScene.Request
            {
                CohortName = $"PM-36965 Range {Guid.NewGuid()}",
                OrgCount = orgCount
            })
        }, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SeedEndpoint_MigrationCohortExportScene_OrgCountLowerBound_Succeeds()
    {
        var cohortName = $"PM-36965 Min {Guid.NewGuid()}";
        var namePrefix = $"pm36965-{Guid.NewGuid():N}-";

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "MigrationCohortExportScene",
            Arguments = JsonSerializer.SerializeToElement(new MigrationCohortExportScene.Request
            {
                CohortName = cohortName,
                OrgCount = 1,
                NamePrefix = namePrefix
            })
        }, Guid.NewGuid().ToString());

        response.EnsureSuccessStatusCode();

        var db = _factory.GetDatabaseContext();
        Assert.Equal(1, await db.Organizations.CountAsync(o => o.Name.StartsWith(namePrefix)));
    }

    private class BatchDeleteResponse
    {
        public string? Message { get; set; }
    }
}

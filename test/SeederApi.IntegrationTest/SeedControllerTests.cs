using System.Net;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Duende.IdentityModel.Client;
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
    public async Task SeedEndpoint_SingleOrganizationScene_WithOverridesAndGateway_ReturnsOk()
    {
        var playId = Guid.NewGuid().ToString();

        // An org owner must already exist with a public key.
        var ownerEmail = $"org-owner-{Guid.NewGuid()}@bitwarden.com";
        var userResponse = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleUserScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(
                new SingleUserScene.Request { Email = ownerEmail, Password = "asdfasdfasdf" })
        }, playId);
        userResponse.EnsureSuccessStatusCode();

        var userResult = await userResponse.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(userResult);
        var ownerUserId = ((System.Text.Json.JsonElement)userResult!.Result!).GetProperty("userId").GetGuid();

        // Seed a Free org but enable SSO via overrides and set the billing gateway triple.
        var orgResponse = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleOrganizationScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new SingleOrganizationScene.Request
            {
                OwnerUserId = ownerUserId,
                PlanType = PlanType.Free,
                Name = "Override Org",
                Domain = "override.example",
                Seats = 5,
                Overrides = new OrganizationOverrides { UseSso = true },
                Gateway = GatewayType.Stripe,
                GatewayCustomerId = "cus_seed_test",
                GatewaySubscriptionId = "sub_seed_test"
            })
        }, playId);

        orgResponse.EnsureSuccessStatusCode();
        var orgResult = await orgResponse.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(orgResult);
        Assert.NotNull(orgResult!.Result);
    }

    private class BatchDeleteResponse
    {
        public string? Message { get; set; }
    }
}

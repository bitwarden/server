using System.Net;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class SeedControllerTests : IClassFixture<SeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly SeederApiApplicationFactory _factory;

    public SeedControllerTests(SeederApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
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
    public async Task SeedEndpoint_WithValidScene_ReturnsOkWithSeedId()
    {
        var testEmail = $"seed-test-{Guid.NewGuid()}@bitwarden.com";

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleUserScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SceneResponseModel>();

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.SeedId);
        Assert.NotNull(result.MangleMap);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task SeedEndpoint_WithInvalidSceneName_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "NonExistentScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = "test@example.com" })
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
    public async Task DeleteEndpoint_WithValidSeedId_ReturnsOk()
    {
        var testEmail = $"delete-test-{Guid.NewGuid()}@bitwarden.com";
        var seedResponse = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleUserScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
        });

        seedResponse.EnsureSuccessStatusCode();
        var seedResult = await seedResponse.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(seedResult);

        var deleteResponse = await _client.DeleteAsync($"/seed/{seedResult.SeedId}");
        deleteResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DeleteEndpoint_WithInvalidSeedId_ReturnsOkWithNull()
    {
        // DestroyRecipe is idempotent - returns null for non-existent seeds
        var nonExistentSeedId = Guid.NewGuid();
        var response = await _client.DeleteAsync($"/seed/{nonExistentSeedId}");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("null", content);
    }

    [Fact]
    public async Task DeleteBatchEndpoint_WithValidSeedIds_ReturnsOk()
    {
        // Create multiple seeds
        var seedIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var testEmail = $"batch-test-{Guid.NewGuid()}@bitwarden.com";
            var seedResponse = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
            {
                Template = "SingleUserScene",
                Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
            });

            seedResponse.EnsureSuccessStatusCode();
            var seedResult = await seedResponse.Content.ReadFromJsonAsync<SceneResponseModel>();
            Assert.NotNull(seedResult);
            Assert.NotNull(seedResult.SeedId);
            seedIds.Add(seedResult.SeedId.Value);
        }

        // Delete them in batch
        var request = new HttpRequestMessage(HttpMethod.Delete, "/seed/batch")
        {
            Content = JsonContent.Create(seedIds)
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
        // Create one valid seed
        var testEmail = $"batch-partial-test-{Guid.NewGuid()}@bitwarden.com";
        var seedResponse = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleUserScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
        });

        seedResponse.EnsureSuccessStatusCode();
        var seedResult = await seedResponse.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(seedResult);

        // Try to delete with mix of valid and invalid IDs
        Assert.NotNull(seedResult.SeedId);
        var seedIds = new List<Guid> { seedResult.SeedId.Value, Guid.NewGuid(), Guid.NewGuid() };
        var request = new HttpRequestMessage(HttpMethod.Delete, "/seed/batch")
        {
            Content = JsonContent.Create(seedIds)
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
            var testEmail = $"deleteall-test-{Guid.NewGuid()}@bitwarden.com";
            var seedResponse = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
            {
                Template = "SingleUserScene",
                Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
            });

            seedResponse.EnsureSuccessStatusCode();
        }

        // Delete all
        var deleteResponse = await _client.DeleteAsync("/seed");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task SeedEndpoint_VerifyResponseContainsSeedIdAndResult()
    {
        var testEmail = $"verify-response-{Guid.NewGuid()}@bitwarden.com";

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleUserScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
        });

        response.EnsureSuccessStatusCode();
        var jsonString = await response.Content.ReadAsStringAsync();

        // Verify the response contains both SeedId and Result fields
        Assert.Contains("seedId", jsonString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("result", jsonString, StringComparison.OrdinalIgnoreCase);
    }

    private class BatchDeleteResponse
    {
        public string? Message { get; set; }
    }
}

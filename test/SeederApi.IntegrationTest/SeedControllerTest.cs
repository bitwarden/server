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
    public async Task SeedEndpoint_WithValidScene_ReturnsOk()
    {
        var testEmail = $"seed-test-{Guid.NewGuid()}@bitwarden.com";
        var playId = Guid.NewGuid().ToString();

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleUserScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
        }, playId);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SceneResponseModel>();

        Assert.NotNull(result);
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
    public async Task DeleteEndpoint_WithValidPlayId_ReturnsOk()
    {
        var testEmail = $"delete-test-{Guid.NewGuid()}@bitwarden.com";
        var playId = Guid.NewGuid().ToString();

        var seedResponse = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = "SingleUserScene",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
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
                Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
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
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
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
                Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
            }, playId);

            var body = await seedResponse.Content.ReadAsStringAsync();
            Console.WriteLine(body);
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
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
        }, playId);

        response.EnsureSuccessStatusCode();
        var jsonString = await response.Content.ReadAsStringAsync();

        // Verify the response contains MangleMap and Result fields
        Assert.Contains("mangleMap", jsonString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("result", jsonString, StringComparison.OrdinalIgnoreCase);
    }

    private class BatchDeleteResponse
    {
        public string? Message { get; set; }
    }
}

using System.Net;
using Bit.SeederApi.Models.Requests;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class EmergencyAccessInviteQueryTests : IClassFixture<SeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly SeederApiApplicationFactory _factory;

    public EmergencyAccessInviteQueryTests(SeederApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task QueryEndpoint_WithValidQueryAndArguments_ReturnsOk()
    {
        var testEmail = $"emergency-test-{Guid.NewGuid()}@bitwarden.com";

        var response = await _client.PostAsJsonAsync("/query", new QueryRequestModel
        {
            Template = "EmergencyAccessInviteQuery",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<QueryResponse>();

        Assert.NotNull(result);
        Assert.NotNull(result.Result);

        // The result should be a JSON array (even if empty for non-existent email)
        var resultElement = result.Result as System.Text.Json.JsonElement?;
        Assert.NotNull(resultElement);

        var urls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(resultElement.Value.GetRawText());
        Assert.NotNull(urls);
        // For a non-existent email, we expect an empty list
        Assert.Empty(urls);
    }

    [Fact]
    public async Task QueryEndpoint_WithInvalidQueryName_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/query", new QueryRequestModel
        {
            Template = "NonExistentQuery",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = "test@example.com" })
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task QueryEndpoint_WithMissingRequiredField_ReturnsBadRequest()
    {
        // EmergencyAccessInviteQuery requires 'email' field
        var response = await _client.PostAsJsonAsync("/query", new QueryRequestModel
        {
            Template = "EmergencyAccessInviteQuery",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { wrongField = "value" })
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task QueryEndpoint_VerifyQueryDoesNotCreateSeedId()
    {
        var testEmail = $"test-{Guid.NewGuid()}@bitwarden.com";

        var response = await _client.PostAsJsonAsync("/query", new QueryRequestModel
        {
            Template = "EmergencyAccessInviteQuery",
            Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { email = testEmail })
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<QueryResponse>();

        Assert.NotNull(result);

        // Verify the response only has Result field, not SeedId
        // (Queries are read-only and don't track entities)
        var jsonString = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("seedId", jsonString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("result", jsonString, StringComparison.OrdinalIgnoreCase);
    }

    private class QueryResponse
    {
        public object? Result { get; set; }
    }
}

using System.Net;
using Bit.SeederApi.Models.Request;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class QueryControllerTests : IClassFixture<SeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly SeederApiApplicationFactory _factory;

    public QueryControllerTests(SeederApiApplicationFactory factory)
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
        var result = await response.Content.ReadAsStringAsync();

        Assert.NotNull(result);

        var urls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(result);
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
}

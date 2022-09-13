using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Core.Entities;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class SecretsControllerTest : IClassFixture<ApiApplicationFactory>
{
    private readonly string _mockEncryptedString = "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";
    private readonly int _secretsToDelete = 3;
    private readonly ApiApplicationFactory _factory;

    public SecretsControllerTest(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task DeleteSecrets()
    {
        var tokens = await _factory.LoginWithNewAccount();
        var client = _factory.CreateClient();

        var orgId = await CreateOrganization(client, tokens.Token);
        var createdSecretIds = new List<Guid>();

        foreach (var i in Enumerable.Range(0, _secretsToDelete))
        {
            var createdSecret = await CreateSecret(orgId, client, tokens.Token);
            createdSecretIds.Add(createdSecret.Id);
        }

        using var message = new HttpRequestMessage(HttpMethod.Delete, "/secrets/bulk")
        {
            Content = new StringContent(JsonSerializer.Serialize(createdSecretIds),
            Encoding.UTF8,
            "application/json"),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
        var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);

        var jsonResult = JsonDocument.Parse(content);
        var index = 0;
        foreach (var element in jsonResult.RootElement.GetProperty("data").EnumerateArray())
        {
            Assert.Equal(createdSecretIds[index].ToString(), element.GetProperty("id").ToString());
            Assert.Empty(element.GetProperty("error").ToString());
            index++;
        }
    }

    private async Task<Secret> CreateSecret(Guid organizationId, HttpClient client, string token)
    {
        var request = new SecretCreateRequestModel()
        {
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/organizations/{organizationId.ToString()}/secrets")
        {
            Content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Secret>();
    }

    private async Task<Guid> CreateOrganization(HttpClient client, string token)
    {
        var request = new OrganizationCreateRequestModel()
        {
            Name = "Integration Test Org",
            BillingEmail = "integration-test@bitwarden.com",
            PlanType = Core.Enums.PlanType.Free,
            Key = "test-key"
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/organizations")
        {
            Content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        return new Guid(JsonDocument.Parse(result).RootElement.GetProperty("id").ToString());
    }
}

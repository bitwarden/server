using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.Models.Request.Organizations;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class SecretsControllerTest : IClassFixture<ApiApplicationFactory>
{
    private readonly string _mockEncryptedString = "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";
    private readonly int _secretsToDelete = 3;
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private ISecretRepository _secretRepository;

    public SecretsControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _secretRepository = _factory.GetService<ISecretRepository>();
    }

    [Fact]
    public async Task DeleteSecrets()
    {
        var tokens = await _factory.LoginWithNewAccount();

        var orgId = await CreateOrganization(tokens.Token);
        var createdSecretIds = new List<Guid>();

        for (var i = 0; i < _secretsToDelete; i++)
        {
            var secret = await _secretRepository.CreateAsync(new Secret
            {
                OrganizationId = orgId,
                Key = _mockEncryptedString,
                Value = _mockEncryptedString,
                Note = _mockEncryptedString
            });
            createdSecretIds.Add(secret.Id);
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "/secrets/delete")
        {
            Content = new StringContent(JsonSerializer.Serialize(createdSecretIds),
            Encoding.UTF8,
            "application/json"),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
        var response = await _client.SendAsync(message);
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

        var secrets = await _secretRepository.GetManyByIds(createdSecretIds);
        Assert.Empty(secrets);
    }

    private async Task<Guid> CreateOrganization(string token)
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
        var response = await _client.SendAsync(message);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        return new Guid(JsonDocument.Parse(result).RootElement.GetProperty("id").ToString());
    }
}

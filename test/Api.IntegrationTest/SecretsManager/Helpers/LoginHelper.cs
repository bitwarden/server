using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.IntegrationTest.SecretsManager.Helpers;

public class LoginHelper
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;

    public LoginHelper(ApiApplicationFactory factory, HttpClient client)
    {
        _factory = factory;
        _client = client;
    }

    public async Task LoginAsync(string email)
    {
        var tokens = await _factory.LoginAsync(email);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
    }

    public async Task LoginWithApiKeyAsync(ApiKeyClientSecretDetails apiKeyDetails)
    {
        var token = await _factory.LoginWithClientSecretAsync(apiKeyDetails.ApiKey.Id, apiKeyDetails.ClientSecret);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _client.DefaultRequestHeaders.Add("service_account_id", apiKeyDetails.ApiKey.ServiceAccountId.ToString());
    }
}

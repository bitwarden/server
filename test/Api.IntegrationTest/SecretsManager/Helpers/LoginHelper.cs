using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.IntegrationTest.SecretsManager.Helpers;

public class LoginHelper(ApiApplicationFactory factory, HttpClient client)
{
    public async Task LoginAsync(string email)
    {
        var tokens = await factory.LoginAsync(email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
    }

    public async Task LoginWithApiKeyAsync(ApiKeyClientSecretDetails apiKeyDetails)
    {
        var token = await factory.LoginWithClientSecretAsync(apiKeyDetails.ApiKey.Id, apiKeyDetails.ClientSecret);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("service_account_id", apiKeyDetails.ApiKey.ServiceAccountId.ToString());
    }
}

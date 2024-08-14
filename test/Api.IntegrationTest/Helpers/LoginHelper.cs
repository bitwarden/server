using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;

namespace Bit.Api.IntegrationTest.Helpers;

public class LoginHelper
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;

    public LoginHelper(ApiApplicationFactory factory, HttpClient client)
    {
        _factory = factory;
        _client = client;
    }

    public async Task LoginWithOrganizationApiKeyAsync(Guid organizationId)
    {
        var (clientId, apiKey) = await GetOrganizationApiKey(_factory, organizationId);
        var token = await _factory.LoginWithOrganizationApiKeyAsync(clientId, apiKey);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _client.DefaultRequestHeaders.Add("client_id", clientId);
    }

    private async Task<(string clientId, string apiKey)> GetOrganizationApiKey<T>(
        WebApplicationFactoryBase<T> factory,
        Guid organizationId)
        where T : class
    {
        var organizationApiKeyRepository = factory.GetService<IOrganizationApiKeyRepository>();
        var apiKeys = await organizationApiKeyRepository.GetManyByOrganizationIdTypeAsync(organizationId);
        var clientId = $"organization.{organizationId}";
        return (clientId, apiKeys.Single().ApiKey);
    }
}

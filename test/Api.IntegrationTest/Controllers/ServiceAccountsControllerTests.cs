using System.Net.Http.Headers;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class ServiceAccountsControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private Organization _organization;

    public ServiceAccountsControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _serviceAccountRepository = _factory.GetService<IServiceAccountRepository>();
    }

    public async Task InitializeAsync()
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        var tokens = await _factory.LoginWithNewAccount(ownerEmail);
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, ownerEmail: ownerEmail, billingEmail: ownerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
        _organization = organization;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetServiceAccountsByOrganization()
    {
        var serviceAccountsToCreate = 3;
        var serviceAccountIds = new List<Guid>();
        for (var i = 0; i < serviceAccountsToCreate; i++)
        {
            var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
            {
                OrganizationId = _organization.Id,
                Name = _mockEncryptedString,
            });
            serviceAccountIds.Add(serviceAccount.Id);
        }

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/service-accounts");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        var jsonResult = JsonDocument.Parse(content);

        Assert.NotEmpty(jsonResult.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(serviceAccountIds.Count(), jsonResult.RootElement.GetProperty("data").EnumerateArray().Count());
    }

    [Fact]
    public async Task CreateServiceAccount()
    {
        var request = new ServiceAccountCreateRequestModel()
        {
            Name = _mockEncryptedString,
        };

        var response = await _client.PostAsJsonAsync($"/organizations/{_organization?.Id}/service-accounts", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ServiceAccount>();

        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);

        var createdServiceAccount = await _serviceAccountRepository.GetByIdAsync(result.Id);
        Assert.NotNull(result);
        Assert.Equal(request.Name, createdServiceAccount.Name);
        AssertHelper.AssertRecent(createdServiceAccount.RevisionDate);
        AssertHelper.AssertRecent(createdServiceAccount.CreationDate);
    }

    [Fact]
    public async Task UpdateServiceAccount()
    {
        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = _organization.Id,
            Name = _mockEncryptedString,
        });

        var request = new ServiceAccountUpdateRequestModel()
        {
            Name = "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=",
        };

        var response = await _client.PutAsJsonAsync($"/service-accounts/{initialServiceAccount.Id}", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ServiceAccount>();
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.NotEqual(initialServiceAccount.Name, result.Name);
        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.NotEqual(initialServiceAccount.RevisionDate, result.RevisionDate);

        var updatedServiceAccount = await _serviceAccountRepository.GetByIdAsync(initialServiceAccount.Id);
        Assert.NotNull(result);
        Assert.Equal(request.Name, updatedServiceAccount.Name);
        AssertHelper.AssertRecent(updatedServiceAccount.RevisionDate);
        AssertHelper.AssertRecent(updatedServiceAccount.CreationDate);
        Assert.NotEqual(initialServiceAccount.Name, updatedServiceAccount.Name);
        Assert.NotEqual(initialServiceAccount.RevisionDate, updatedServiceAccount.RevisionDate);
    }

    [Fact]
    public async Task CreateServiceAccountAccessToken()
    {
        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = _organization.Id,
            Name = _mockEncryptedString,
        });

        var mockExpiresAt = DateTime.UtcNow.AddDays(30);
        var request = new AccessTokenCreateRequestModel()
        {
            Name = _mockEncryptedString,
            EncryptedPayload = _mockEncryptedString,
            Key = _mockEncryptedString,
            ExpireAt = mockExpiresAt
        };

        var response = await _client.PostAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-tokens", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiKey>();

        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.NotNull(result.ClientSecret);
        Assert.Equal(mockExpiresAt, result.ExpireAt);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);
    }

    [Fact]
    public async Task CreateServiceAccountAccessTokenExpireAtNullAsync()
    {
        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = _organization.Id,
            Name = _mockEncryptedString,
        });

        var request = new AccessTokenCreateRequestModel()
        {
            Name = _mockEncryptedString,
            EncryptedPayload = _mockEncryptedString,
            Key = _mockEncryptedString,
            ExpireAt = null
        };

        var response = await _client.PostAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-tokens", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiKey>();

        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.NotNull(result.ClientSecret);
        Assert.Null(result.ExpireAt);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);
    }
}

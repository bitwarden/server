using System.Net;
using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Enums;
using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

public class ServiceAccountsControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private const string _mockNewName =
        "2.3AZ+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;

    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public ServiceAccountsControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _serviceAccountRepository = _factory.GetService<IServiceAccountRepository>();
        _accessPolicyRepository = _factory.GetService<IAccessPolicyRepository>();
        _apiKeyRepository = _factory.GetService<IApiKeyRepository>();
    }

    public async Task InitializeAsync()
    {
        _email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_email);
        _organizationHelper = new SecretsManagerOrganizationHelper(_factory, _email);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private async Task LoginAsync(string email)
    {
        var tokens = await _factory.LoginAsync(email);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ListByOrganization_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);

        var response = await _client.GetAsync($"/organizations/{org.Id}/service-accounts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListByOrganization_Admin_Success()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);

        var serviceAccountIds = await SetupGetServiceAccountsByOrganizationAsync(org);

        var response = await _client.GetAsync($"/organizations/{org.Id}/service-accounts");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<ServiceAccountResponseModel>>();

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Data);
        Assert.Equal(serviceAccountIds.Count, result.Data.Count());
    }

    [Fact]
    public async Task ListByOrganization_User_Success()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var serviceAccountIds = await SetupGetServiceAccountsByOrganizationAsync(org);

        // Setup access for two
        var accessPolicies = serviceAccountIds.Take(2).Select(
            id => new UserServiceAccountAccessPolicy
            {
                OrganizationUserId = orgUser.Id,
                GrantedServiceAccountId = id,
                Read = true,
                Write = false,
            }).Cast<BaseAccessPolicy>().ToList();

        await _accessPolicyRepository.CreateManyAsync(accessPolicies);

        var response = await _client.GetAsync($"/organizations/{org.Id}/service-accounts");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<ServiceAccountResponseModel>>();

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Data);
        Assert.Equal(2, result.Data.Count());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Create_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);

        var request = new ServiceAccountCreateRequestModel { Name = _mockEncryptedString };

        var response = await _client.PostAsJsonAsync($"/organizations/{org.Id}/service-accounts", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_Admin_Success()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);

        var request = new ServiceAccountCreateRequestModel { Name = _mockEncryptedString };

        var response = await _client.PostAsJsonAsync($"/organizations/{org.Id}/service-accounts", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ServiceAccountResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(request.Name, result!.Name);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);

        var createdServiceAccount = await _serviceAccountRepository.GetByIdAsync(new Guid(result.Id));
        Assert.NotNull(result);
        Assert.Equal(request.Name, createdServiceAccount.Name);
        AssertHelper.AssertRecent(createdServiceAccount.RevisionDate);
        AssertHelper.AssertRecent(createdServiceAccount.CreationDate);

        // Check permissions have been bootstrapped.
        var accessPolicies = await _accessPolicyRepository.GetManyByGrantedServiceAccountIdAsync(createdServiceAccount.Id);
        Assert.NotNull(accessPolicies);
        var ap = accessPolicies!.First();
        Assert.True(ap.Read);
        Assert.True(ap.Write);
        AssertHelper.AssertRecent(ap.CreationDate);
        AssertHelper.AssertRecent(ap.RevisionDate);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Update_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);

        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var request = new ServiceAccountUpdateRequestModel { Name = _mockNewName };

        var response = await _client.PutAsJsonAsync($"/service-accounts/{initialServiceAccount.Id}", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_Admin()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);

        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var request = new ServiceAccountUpdateRequestModel { Name = _mockNewName };

        var response = await _client.PutAsJsonAsync($"/service-accounts/{initialServiceAccount.Id}", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ServiceAccountResponseModel>();
        Assert.NotNull(result);
        Assert.Equal(request.Name, result!.Name);
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
    public async Task Update_User_WithPermission()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        await CreateUserPolicyAsync(orgUser.Id, initialServiceAccount.Id, true, true);

        var request = new ServiceAccountUpdateRequestModel { Name = _mockNewName };

        var response = await _client.PutAsJsonAsync($"/service-accounts/{initialServiceAccount.Id}", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ServiceAccountResponseModel>();
        Assert.NotNull(result);
        Assert.Equal(request.Name, result!.Name);
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
    public async Task Update_User_NoPermissions()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var request = new ServiceAccountUpdateRequestModel { Name = _mockNewName };

        var response = await _client.PutAsJsonAsync($"/service-accounts/{initialServiceAccount.Id}", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CreateAccessToken_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var mockExpiresAt = DateTime.UtcNow.AddDays(30);
        var request = new AccessTokenCreateRequestModel
        {
            Name = _mockEncryptedString,
            EncryptedPayload = _mockEncryptedString,
            Key = _mockEncryptedString,
            ExpireAt = mockExpiresAt,
        };

        var response = await _client.PostAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-tokens", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAccessToken_Admin()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var mockExpiresAt = DateTime.UtcNow.AddDays(30);
        var request = new AccessTokenCreateRequestModel
        {
            Name = _mockEncryptedString,
            EncryptedPayload = _mockEncryptedString,
            Key = _mockEncryptedString,
            ExpireAt = mockExpiresAt,
        };

        var response = await _client.PostAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-tokens", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AccessTokenCreationResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(request.Name, result!.Name);
        Assert.NotNull(result.ClientSecret);
        Assert.Equal(mockExpiresAt, result.ExpireAt);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);
    }

    [Fact]
    public async Task CreateAccessToken_User_WithPermission()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        await CreateUserPolicyAsync(orgUser.Id, serviceAccount.Id, true, true);

        var mockExpiresAt = DateTime.UtcNow.AddDays(30);
        var request = new AccessTokenCreateRequestModel
        {
            Name = _mockEncryptedString,
            EncryptedPayload = _mockEncryptedString,
            Key = _mockEncryptedString,
            ExpireAt = mockExpiresAt,
        };

        var response = await _client.PostAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-tokens", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AccessTokenCreationResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(request.Name, result!.Name);
        Assert.NotNull(result.ClientSecret);
        Assert.Equal(mockExpiresAt, result.ExpireAt);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);
    }

    [Fact]
    public async Task CreateAccessToken_User_NoPermission()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var mockExpiresAt = DateTime.UtcNow.AddDays(30);
        var request = new AccessTokenCreateRequestModel
        {
            Name = _mockEncryptedString,
            EncryptedPayload = _mockEncryptedString,
            Key = _mockEncryptedString,
            ExpireAt = mockExpiresAt,
        };

        var response = await _client.PostAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-tokens", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAccessToken_ExpireAtNull_Admin()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var request = new AccessTokenCreateRequestModel
        {
            Name = _mockEncryptedString,
            EncryptedPayload = _mockEncryptedString,
            Key = _mockEncryptedString,
            ExpireAt = null,
        };

        var response = await _client.PostAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-tokens", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AccessTokenCreationResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(request.Name, result!.Name);
        Assert.NotNull(result.ClientSecret);
        Assert.Null(result.ExpireAt);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RevokeAccessToken_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var accessToken = await _apiKeyRepository.CreateAsync(new ApiKey
        {
            ServiceAccountId = org.Id,
            Name = _mockEncryptedString,
            ExpireAt = DateTime.UtcNow.AddDays(30),
        });

        var request = new RevokeAccessTokensRequest
        {
            Ids = new[] { accessToken.Id },
        };

        var response = await _client.PostAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-tokens/revoke", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RevokeAccessToken_User_NoPermission()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var accessToken = await _apiKeyRepository.CreateAsync(new ApiKey
        {
            ServiceAccountId = org.Id,
            Name = _mockEncryptedString,
            ExpireAt = DateTime.UtcNow.AddDays(30),
        });

        var request = new RevokeAccessTokensRequest
        {
            Ids = new[] { accessToken.Id },
        };

        var response = await _client.PostAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-tokens/revoke", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task RevokeAccessToken_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        if (permissionType == PermissionType.RunAsAdmin)
        {
            await LoginAsync(_email);
        }
        else
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await LoginAsync(email);

            await _accessPolicyRepository.CreateManyAsync(new List<BaseAccessPolicy> {
                new UserServiceAccountAccessPolicy
                {
                    GrantedServiceAccountId = serviceAccount.Id,
                    OrganizationUserId = orgUser.Id,
                    Write = true,
                    Read = true,
                },
            });
        }

        var accessToken = await _apiKeyRepository.CreateAsync(new ApiKey
        {
            ServiceAccountId = org.Id,
            Name = _mockEncryptedString,
            ExpireAt = DateTime.UtcNow.AddDays(30),
        });

        var request = new RevokeAccessTokensRequest
        {
            Ids = new[] { accessToken.Id },
        };

        var response = await _client.PostAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-tokens/revoke", request);
        response.EnsureSuccessStatusCode();
    }

    private async Task CreateUserPolicyAsync(Guid userId, Guid serviceAccountId, bool read, bool write)
    {
        var policy = new UserServiceAccountAccessPolicy
        {
            OrganizationUserId = userId,
            GrantedServiceAccountId = serviceAccountId,
            Read = read,
            Write = write,
        };
        await _accessPolicyRepository.CreateManyAsync(new List<BaseAccessPolicy> { policy });
    }

    private async Task<List<Guid>> SetupGetServiceAccountsByOrganizationAsync(Organization org)
    {
        var serviceAccountIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
            {
                OrganizationId = org.Id,
                Name = _mockEncryptedString,
            });
            serviceAccountIds.Add(serviceAccount.Id);
        }

        return serviceAccountIds;
    }
}

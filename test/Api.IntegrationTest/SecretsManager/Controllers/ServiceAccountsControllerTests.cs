using System.Net;
using System.Text.Json.Nodes;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Enums;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

public class ServiceAccountsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private const string _mockNewName =
        "2.3AZ+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public ServiceAccountsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _serviceAccountRepository = _factory.GetService<IServiceAccountRepository>();
        _accessPolicyRepository = _factory.GetService<IAccessPolicyRepository>();
        _apiKeyRepository = _factory.GetService<IApiKeyRepository>();
        _secretRepository = _factory.GetService<ISecretRepository>();
        _projectRepository = _factory.GetService<IProjectRepository>();
        _loginHelper = new LoginHelper(_factory, _client);
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

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task ListByOrganization_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var response = await _client.GetAsync($"/organizations/{org.Id}/service-accounts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task ListByOrganization_NoSecretAccess_Success(PermissionType permissionType)
    {
        var (orgId, serviceAccountIds) = await SetupListByOrganizationRequestAsync(permissionType);

        var response = await _client.GetAsync($"/organizations/{orgId}/service-accounts");
        response.EnsureSuccessStatusCode();
        var result = await response.Content
            .ReadFromJsonAsync<ListResponseModel<ServiceAccountSecretsDetailsResponseModel>>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Data);
        Assert.Equal(serviceAccountIds.Count, result.Data.Count());
        Assert.DoesNotContain(result.Data, x => x.AccessToSecrets != 0);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task ListByOrganization_SecretAccess_Success(PermissionType permissionType)
    {
        var (orgId, serviceAccountIds) = await SetupListByOrganizationRequestAsync(permissionType);
        var expectedAccess = await SetupServiceAccountSecretAccessAsync(serviceAccountIds, orgId);

        var response = await _client.GetAsync($"/organizations/{orgId}/service-accounts?includeAccessToSecrets=true");
        response.EnsureSuccessStatusCode();
        var result = await response.Content
            .ReadFromJsonAsync<ListResponseModel<ServiceAccountSecretsDetailsResponseModel>>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Data);
        Assert.Equal(serviceAccountIds.Count, result.Data.Count());

        foreach (var item in expectedAccess)
        {
            var serviceAccountResult = result.Data.FirstOrDefault(x => x.Id == item.Key);
            Assert.NotNull(serviceAccountResult);
            Assert.Equal(item.Value, serviceAccountResult.AccessToSecrets);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListByOrganization_UserPartialAccess_ReturnsServiceAccountsUserHasAccessTo(
        bool includeAccessToSecrets)
    {
        var (orgId, serviceAccountIds) =
            await SetupListByOrganizationRequestAsync(PermissionType.RunAsUserWithPermission);
        var expectedAccess = await SetupServiceAccountSecretAccessAsync(serviceAccountIds, orgId);

        var serviceAccountWithoutAccess = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = orgId,
            Name = _mockEncryptedString
        });

        var response =
            await _client.GetAsync(
                $"/organizations/{orgId}/service-accounts?includeAccessToSecrets={includeAccessToSecrets}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content
            .ReadFromJsonAsync<ListResponseModel<ServiceAccountSecretsDetailsResponseModel>>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Data);
        Assert.Equal(serviceAccountIds.Count, result.Data.Count());
        Assert.DoesNotContain(result.Data, x => x.Id == serviceAccountWithoutAccess.Id);

        if (includeAccessToSecrets)
        {
            foreach (var item in expectedAccess)
            {
                var serviceAccountResult = result.Data.FirstOrDefault(x => x.Id == item.Key);
                Assert.NotNull(serviceAccountResult);
                Assert.Equal(item.Value, serviceAccountResult.AccessToSecrets);
            }
        }
        else
        {
            Assert.Contains(result.Data, x => x.AccessToSecrets == 0);
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetByServiceAccountId_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response = await _client.GetAsync($"/service-accounts/{serviceAccount.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByServiceAccountId_ServiceAccountDoesNotExist_NotFound()
    {
        await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var response = await _client.GetAsync($"/service-accounts/{new Guid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByServiceAccountId_UserWithoutPermission_NotFound()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response = await _client.GetAsync($"/service-accounts/{serviceAccount.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetByServiceAccountId_Success(PermissionType permissionType)
    {
        var serviceAccount = await SetupServiceAccountWithAccessAsync(permissionType);

        var response = await _client.GetAsync($"/service-accounts/{serviceAccount.Id}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ServiceAccountResponseModel>();
        Assert.NotNull(result);
        Assert.Equal(serviceAccount.Id, result.Id);
        Assert.Equal(serviceAccount.OrganizationId, result.OrganizationId);
        Assert.Equal(serviceAccount.Name, result.Name);
        Assert.Equal(serviceAccount.CreationDate, result.CreationDate);
        Assert.Equal(serviceAccount.RevisionDate, result.RevisionDate);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Create_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var request = new ServiceAccountCreateRequestModel { Name = _mockEncryptedString };

        var response = await _client.PostAsJsonAsync($"/organizations/{org.Id}/service-accounts", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task Create_Success(PermissionType permissionType)
    {
        var (org, adminOrgUser) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var orgUserId = adminOrgUser.Id;
        var currentUserId = adminOrgUser.UserId!.Value;

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);
            orgUserId = orgUser.Id;
            currentUserId = orgUser.UserId!.Value;
        }

        var request = new ServiceAccountCreateRequestModel { Name = _mockEncryptedString };

        var response = await _client.PostAsJsonAsync($"/organizations/{org.Id}/service-accounts", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ServiceAccountResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);

        var createdServiceAccount = await _serviceAccountRepository.GetByIdAsync(result.Id);
        Assert.NotNull(result);
        Assert.Equal(request.Name, createdServiceAccount.Name);
        AssertHelper.AssertRecent(createdServiceAccount.RevisionDate);
        AssertHelper.AssertRecent(createdServiceAccount.CreationDate);

        // Check permissions have been bootstrapped.
        var accessPolicies = await _accessPolicyRepository.GetPeoplePoliciesByGrantedServiceAccountIdAsync(createdServiceAccount.Id, currentUserId);
        Assert.NotNull(accessPolicies);
        var ap = (UserServiceAccountAccessPolicy)accessPolicies.First();
        Assert.Equal(createdServiceAccount.Id, ap.GrantedServiceAccountId);
        Assert.Equal(orgUserId, ap.OrganizationUserId);
        Assert.True(ap.Read);
        Assert.True(ap.Write);
        AssertHelper.AssertRecent(ap.CreationDate);
        AssertHelper.AssertRecent(ap.RevisionDate);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Update_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

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
    public async Task Update_User_NoPermissions()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

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
    public async Task Update_NonExistingServiceAccount_NotFound()
    {
        await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var request = new ServiceAccountUpdateRequestModel { Name = _mockNewName };

        var response = await _client.PutAsJsonAsync("/service-accounts/c53de509-4581-402c-8cbd-f26d2c516fba", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task Update_Success(PermissionType permissionType)
    {
        var initialServiceAccount = await SetupServiceAccountWithAccessAsync(permissionType);

        var request = new ServiceAccountUpdateRequestModel { Name = _mockNewName };

        var response = await _client.PutAsJsonAsync($"/service-accounts/{initialServiceAccount.Id}", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ServiceAccountResponseModel>();
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

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Delete_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var request = new List<Guid> { initialServiceAccount.Id };

        var response = await _client.PutAsJsonAsync("/service-accounts/delete", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_MissingAccessPolicy_AccessDenied()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var ids = new List<Guid> { serviceAccount.Id };

        var response = await _client.PostAsJsonAsync("/service-accounts/delete", ids);

        var results = await response.Content.ReadFromJsonAsync<ListResponseModel<BulkDeleteResponseModel>>();
        Assert.NotNull(results);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task Delete_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        await _apiKeyRepository.CreateAsync(CreateTestApiKey(serviceAccount.Id));

        if (permissionType == PermissionType.RunAsAdmin)
        {
            await _loginHelper.LoginAsync(_email);
        }
        else
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

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

        var ids = new List<Guid> { serviceAccount.Id };

        var response = await _client.PostAsJsonAsync("/service-accounts/delete", ids);
        response.EnsureSuccessStatusCode();

        var sa = await _serviceAccountRepository.GetManyByIds(ids);
        Assert.Empty(sa);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetAccessTokens_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response = await _client.GetAsync($"/service-accounts/{serviceAccount.Id}/access-tokens");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAccessTokens_UserNoPermission_NotFound()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        await _apiKeyRepository.CreateAsync(
            CreateTestApiKey(serviceAccount.Id, _mockEncryptedString, DateTime.UtcNow.AddDays(30))
        );

        var response = await _client.GetAsync($"/service-accounts/{serviceAccount.Id}/access-tokens");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetAccessTokens_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

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

        var accessToken = await _apiKeyRepository.CreateAsync(
            CreateTestApiKey(serviceAccount.Id, _mockEncryptedString, DateTime.UtcNow.AddDays(30))
        );


        var response = await _client.GetAsync($"/service-accounts/{serviceAccount.Id}/access-tokens");
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<ListResponseModel<AccessTokenResponseModel>>();
        Assert.NotEmpty(results!.Data);
        Assert.Equal(accessToken.Id, results.Data.First().Id);
        Assert.Equal(accessToken.Name, results.Data.First().Name);
        Assert.Equal(accessToken.GetScopes(), results.Data.First().Scopes);
        Assert.Equal(accessToken.ExpireAt, results.Data.First().ExpireAt);
        Assert.Equal(accessToken.CreationDate, results.Data.First().CreationDate);
        Assert.Equal(accessToken.RevisionDate, results.Data.First().RevisionDate);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task CreateAccessToken_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

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
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

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
        Assert.Equal(request.Name, result.Name);
        Assert.NotNull(result.ClientSecret);
        Assert.Equal(mockExpiresAt, result.ExpireAt);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);
    }

    [Fact]
    public async Task CreateAccessToken_User_WithPermission()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

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
        Assert.Equal(request.Name, result.Name);
        Assert.NotNull(result.ClientSecret);
        Assert.Equal(mockExpiresAt, result.ExpireAt);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);
    }

    [Fact]
    public async Task CreateAccessToken_User_NoPermission()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

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
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

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
        Assert.Equal(request.Name, result.Name);
        Assert.NotNull(result.ClientSecret);
        Assert.Null(result.ExpireAt);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task RevokeAccessToken_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString
        });

        var accessToken = await _apiKeyRepository.CreateAsync(
            CreateTestApiKey(serviceAccount.Id, _mockEncryptedString, DateTime.UtcNow.AddDays(30))
        );

        var request = new RevokeAccessTokensRequest
        {
            Ids = new[] { accessToken.Id },
        };

        var response = await _client.PostAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-tokens/revoke", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevokeAccessToken_User_NoPermission(bool hasReadAccess)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        if (hasReadAccess)
        {
            await _accessPolicyRepository.CreateManyAsync(new List<BaseAccessPolicy> {
                new UserServiceAccountAccessPolicy
                {
                    GrantedServiceAccountId = serviceAccount.Id,
                    OrganizationUserId = orgUser.Id,
                    Write = false,
                    Read = true,
                },
            });
        }

        var accessToken = await _apiKeyRepository.CreateAsync(
            CreateTestApiKey(serviceAccount.Id, _mockEncryptedString, DateTime.UtcNow.AddDays(30))
        );

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
        var (org, _) = await _organizationHelper.Initialize(true, true, true);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        if (permissionType == PermissionType.RunAsAdmin)
        {
            await _loginHelper.LoginAsync(_email);
        }
        else
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

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

        var accessToken = await _apiKeyRepository.CreateAsync(
            CreateTestApiKey(serviceAccount.Id, _mockEncryptedString, DateTime.UtcNow.AddDays(30))
        );

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

    private async Task<List<Guid>> CreateServiceAccountsInOrganizationAsync(Organization org)
    {
        var serviceAccountIds = new List<Guid>();
        for (var i = 0; i < 4; i++)
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

    private async Task<ServiceAccount> SetupServiceAccountWithAccessAsync(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        if (permissionType == PermissionType.RunAsAdmin)
        {
            return initialServiceAccount;
        }

        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var accessPolicies = new List<BaseAccessPolicy>
        {
            new UserServiceAccountAccessPolicy
            {
                GrantedServiceAccountId = initialServiceAccount.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true,
            },
        };
        await _accessPolicyRepository.CreateManyAsync(accessPolicies);

        return initialServiceAccount;
    }

    private async Task<(Guid OrganizationId, List<Guid> ServiceAccountIds)> SetupListByOrganizationRequestAsync(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var serviceAccountIds = await CreateServiceAccountsInOrganizationAsync(org);

        if (permissionType == PermissionType.RunAsAdmin)
        {
            return (org.Id, serviceAccountIds);
        }

        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var accessPolicies = serviceAccountIds.Select(
            id => new UserServiceAccountAccessPolicy
            {
                OrganizationUserId = orgUser.Id,
                GrantedServiceAccountId = id,
                Read = true,
                Write = false,
            }).Cast<BaseAccessPolicy>().ToList();

        await _accessPolicyRepository.CreateManyAsync(accessPolicies);

        return (org.Id, serviceAccountIds);
    }

    private static ApiKey CreateTestApiKey(Guid serviceAccountId, string name = _mockEncryptedString, DateTime? expiresAt = null)
    {
        return new ApiKey
        {
            ServiceAccountId = serviceAccountId,
            Name = name,
            ExpireAt = expiresAt,
            Scope = new JsonArray
            {
                "api.secrets",
            }.ToJsonString(),
            EncryptedPayload = _mockEncryptedString,
            Key = _mockEncryptedString,
        };
    }

    private async Task<Dictionary<Guid, int>> SetupServiceAccountSecretAccessAsync(List<Guid> serviceAccountIds,
        Guid organizationId)
    {
        var project =
            await _projectRepository.CreateAsync(new Project
            {
                Name = _mockEncryptedString,
                OrganizationId = organizationId
            });

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            OrganizationId = organizationId,
            Projects = [project]
        });

        var secretNoProject = await _secretRepository.CreateAsync(new Secret
        {
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            OrganizationId = organizationId
        });

        var serviceAccountWithProjectAccess = serviceAccountIds[0];
        var serviceAccountWithProjectAndSecretAccess = serviceAccountIds[1];
        var serviceAccountWithSecretAccess = serviceAccountIds[2];
        var serviceAccountWithNoAccess = serviceAccountIds[3];
        await _accessPolicyRepository.CreateManyAsync([
            new ServiceAccountProjectAccessPolicy
            {
                ServiceAccountId = serviceAccountWithProjectAccess,
                GrantedProjectId = project.Id,
                Read = true,
                Write = true
            },
            new ServiceAccountProjectAccessPolicy
            {
                ServiceAccountId = serviceAccountWithProjectAndSecretAccess,
                GrantedProjectId = project.Id,
                Read = true,
                Write = true
            },
            new ServiceAccountSecretAccessPolicy
            {
                ServiceAccountId = serviceAccountWithProjectAndSecretAccess,
                GrantedSecretId = secret.Id,
                Read = true,
                Write = true
            },
            new ServiceAccountSecretAccessPolicy
            {
                ServiceAccountId = serviceAccountWithProjectAndSecretAccess,
                GrantedSecretId = secretNoProject.Id,
                Read = true,
                Write = true
            },
            new ServiceAccountSecretAccessPolicy
            {
                ServiceAccountId = serviceAccountWithSecretAccess,
                GrantedSecretId = secretNoProject.Id,
                Read = true,
                Write = true
            }
        ]);

        return new Dictionary<Guid, int>
        {
            { serviceAccountWithProjectAccess, 1 },
            { serviceAccountWithProjectAndSecretAccess, 2 },
            { serviceAccountWithSecretAccess, 1 },
            { serviceAccountWithNoAccess, 0 }
        };
    }
}

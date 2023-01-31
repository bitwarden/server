using System.Net;
using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Enums;
using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

public class AccessPoliciesControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly IAccessPolicyRepository _accessPolicyRepository;

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public AccessPoliciesControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _accessPolicyRepository = _factory.GetService<IAccessPolicyRepository>();
        _serviceAccountRepository = _factory.GetService<IServiceAccountRepository>();
        _projectRepository = _factory.GetService<IProjectRepository>();
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
    public async Task CreateProjectAccessPolicies_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);

        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var request = new AccessPoliciesCreateRequest
        {
            ServiceAccountAccessPolicyRequests = new List<AccessPolicyRequest>
            {
                new() { GranteeId = initialServiceAccount.Id, Read = true, Write = true },
            },
        };

        var response = await _client.PostAsJsonAsync($"/projects/{initialProject.Id}/access-policies", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task CreateProjectAccessPolicies(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);

        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await LoginAsync(email);
            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = initialProject.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var request = new AccessPoliciesCreateRequest
        {
            ServiceAccountAccessPolicyRequests = new List<AccessPolicyRequest>
            {
                new() { GranteeId = initialServiceAccount.Id, Read = true, Write = true },
            },
        };

        var response = await _client.PostAsJsonAsync($"/projects/{initialProject.Id}/access-policies", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectAccessPoliciesResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(initialServiceAccount.Id, result!.ServiceAccountAccessPolicies.First().ServiceAccountId);
        Assert.True(result.ServiceAccountAccessPolicies.First().Read);
        Assert.True(result.ServiceAccountAccessPolicies.First().Write);
        AssertHelper.AssertRecent(result.ServiceAccountAccessPolicies.First().RevisionDate);
        AssertHelper.AssertRecent(result.ServiceAccountAccessPolicies.First().CreationDate);

        var createdAccessPolicy =
            await _accessPolicyRepository.GetByIdAsync(result.ServiceAccountAccessPolicies.First().Id);
        Assert.NotNull(createdAccessPolicy);
        Assert.Equal(result.ServiceAccountAccessPolicies.First().Read, createdAccessPolicy!.Read);
        Assert.Equal(result.ServiceAccountAccessPolicies.First().Write, createdAccessPolicy.Write);
        Assert.Equal(result.ServiceAccountAccessPolicies.First().Id, createdAccessPolicy.Id);
        AssertHelper.AssertRecent(createdAccessPolicy.CreationDate);
        AssertHelper.AssertRecent(createdAccessPolicy.RevisionDate);
    }

    [Fact]
    public async Task CreateProjectAccessPolicies_NoPermission()
    {
        // Create a new account as a user
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var request = new AccessPoliciesCreateRequest
        {
            ServiceAccountAccessPolicyRequests = new List<AccessPolicyRequest>
            {
                new() { GranteeId = initialServiceAccount.Id, Read = true, Write = true },
            },
        };

        var response = await _client.PostAsJsonAsync($"/projects/{initialProject.Id}/access-policies", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task UpdateAccessPolicy_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        const bool expectedRead = true;
        const bool expectedWrite = false;
        var request = new AccessPolicyUpdateRequest { Read = expectedRead, Write = expectedWrite };

        var response = await _client.PutAsJsonAsync($"/access-policies/{initData.InitialAccessPolicyId}", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAccessPolicy_NoPermission()
    {
        // Create a new account as a user
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var initData = await SetupAccessPolicyRequest(orgUser.OrganizationId);

        const bool expectedRead = true;
        const bool expectedWrite = false;
        var request = new AccessPolicyUpdateRequest { Read = expectedRead, Write = expectedWrite };

        var response = await _client.PutAsJsonAsync($"/access-policies/{initData.InitialAccessPolicyId}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task UpdateAccessPolicy(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await LoginAsync(email);
            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = initData.InitialProjectId,
                    OrganizationUserId = orgUser.Id,
                    Read = true,
                    Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        const bool expectedRead = true;
        const bool expectedWrite = false;
        var request = new AccessPolicyUpdateRequest { Read = expectedRead, Write = expectedWrite };

        var response = await _client.PutAsJsonAsync($"/access-policies/{initData.InitialAccessPolicyId}", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ServiceAccountProjectAccessPolicyResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(expectedRead, result!.Read);
        Assert.Equal(expectedWrite, result.Write);
        AssertHelper.AssertRecent(result.RevisionDate);

        var updatedAccessPolicy = await _accessPolicyRepository.GetByIdAsync(result.Id);
        Assert.NotNull(updatedAccessPolicy);
        Assert.Equal(expectedRead, updatedAccessPolicy!.Read);
        Assert.Equal(expectedWrite, updatedAccessPolicy.Write);
        AssertHelper.AssertRecent(updatedAccessPolicy.RevisionDate);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DeleteAccessPolicy_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        var response = await _client.DeleteAsync($"/access-policies/{initData.InitialAccessPolicyId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccessPolicy_NoPermission()
    {
        // Create a new account as a user
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var initData = await SetupAccessPolicyRequest(orgUser.OrganizationId);

        var response = await _client.DeleteAsync($"/access-policies/{initData.InitialAccessPolicyId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task DeleteAccessPolicy(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await LoginAsync(email);
            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = initData.InitialProjectId,
                    OrganizationUserId = orgUser.Id,
                    Read = true,
                    Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var response = await _client.DeleteAsync($"/access-policies/{initData.InitialAccessPolicyId}");
        response.EnsureSuccessStatusCode();

        var test = await _accessPolicyRepository.GetByIdAsync(initData.InitialAccessPolicyId);
        Assert.Null(test);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GetProjectAccessPolicies_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        var response = await _client.GetAsync($"/projects/{initData.InitialProjectId}/access-policies");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectAccessPolicies_NoPermission()
    {
        // Create a new account as a user
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var initData = await SetupAccessPolicyRequest(orgUser.OrganizationId);

        var response = await _client.GetAsync($"/projects/{initData.InitialProjectId}/access-policies");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetProjectAccessPolicies(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await LoginAsync(email);
            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = initData.InitialProjectId,
                    OrganizationUserId = orgUser.Id,
                    Read = true,
                    Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var response = await _client.GetAsync($"/projects/{initData.InitialProjectId}/access-policies");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectAccessPoliciesResponseModel>();

        Assert.NotNull(result?.ServiceAccountAccessPolicies);
        Assert.Single(result!.ServiceAccountAccessPolicies);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GetProjectServiceAccountPotentialGrantees_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);

        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response =
            await _client.GetAsync(
                $"/projects/{initialProject.Id}/access-policies/service-accounts/potential-grantees");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectServiceAccountPotentialGrantees_NoPermission()
    {
        // Create a new account as a user
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });


        var response =
            await _client.GetAsync(
                $"/projects/{initialProject.Id}/access-policies/service-accounts/potential-grantees");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetProjectServiceAccountPotentialGrantees(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);

        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await LoginAsync(email);
            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = initialProject.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                },
                new UserServiceAccountAccessPolicy
                {
                    GrantedServiceAccountId = initialServiceAccount.Id,
                    OrganizationUserId = orgUser.Id,
                    Read = true,
                    Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var response =
            await _client.GetAsync(
                $"/projects/{initialProject.Id}/access-policies/service-accounts/potential-grantees");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<PotentialGranteeResponseModel>>();

        Assert.NotNull(result?.Data);
        Assert.Single(result!.Data);
        Assert.Equal(initialServiceAccount.Id.ToString(), result.Data.First().Id);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GetProjectPeoplePotentialGrantees_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);

        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response =
            await _client.GetAsync($"/projects/{initialProject.Id}/access-policies/people/potential-grantees");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectPeoplePotentialGrantees_NoPermission()
    {
        // Create a new account as a user
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response =
            await _client.GetAsync($"/projects/{initialProject.Id}/access-policies/people/potential-grantees");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetProjectPeoplePotentialGrantees(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);

        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await LoginAsync(email);
            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = initialProject.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var response =
            await _client.GetAsync($"/projects/{initialProject.Id}/access-policies/people/potential-grantees");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<PotentialGranteeResponseModel>>();

        // Will contain user accounts created for integration tests
        Assert.NotNull(result?.Data);
    }

    private async Task<RequestSetupData> SetupAccessPolicyRequest(Guid organizationId)
    {
        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = organizationId,
            Name = _mockEncryptedString,
        });

        var initialServiceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = organizationId,
            Name = _mockEncryptedString,
        });

        var initialAccessPolicy = await _accessPolicyRepository.CreateManyAsync(
            new List<BaseAccessPolicy>
            {
                new ServiceAccountProjectAccessPolicy
                {
                    Read = true,
                    Write = true,
                    ServiceAccountId = initialServiceAccount.Id,
                    GrantedProjectId = initialProject.Id,
                },
            });

        return new RequestSetupData
        {
            InitialProjectId = initialProject.Id,
            InitialServiceAccountId = initialServiceAccount.Id,
            InitialAccessPolicyId = initialAccessPolicy.First().Id,
        };
    }

    private class RequestSetupData
    {
        public Guid InitialProjectId { get; set; }
        public Guid InitialAccessPolicyId { get; set; }
        public Guid InitialServiceAccountId { get; set; }
    }
}

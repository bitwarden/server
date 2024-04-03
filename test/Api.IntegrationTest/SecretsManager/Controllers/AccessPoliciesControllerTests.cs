using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Enums;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

public class AccessPoliciesControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly IAccessPolicyRepository _accessPolicyRepository;

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly LoginHelper _loginHelper;
    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public AccessPoliciesControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _accessPolicyRepository = _factory.GetService<IAccessPolicyRepository>();
        _serviceAccountRepository = _factory.GetService<IServiceAccountRepository>();
        _projectRepository = _factory.GetService<IProjectRepository>();
        _groupRepository = _factory.GetService<IGroupRepository>();
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
    public async Task CreateProjectAccessPolicies_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var (projectId, serviceAccountId) = await CreateProjectAndServiceAccountAsync(org.Id);

        var request = new AccessPoliciesCreateRequest
        {
            ServiceAccountAccessPolicyRequests = new List<AccessPolicyRequest>
            {
                new() { GranteeId = serviceAccountId, Read = true, Write = true },
            },
        };

        var response = await _client.PostAsJsonAsync($"/projects/{projectId}/access-policies", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateProjectAccessPolicies_NoPermission()
    {
        // Create a new account as a user
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var (projectId, serviceAccountId) = await CreateProjectAndServiceAccountAsync(org.Id);
        var request = new AccessPoliciesCreateRequest
        {
            ServiceAccountAccessPolicyRequests = new List<AccessPolicyRequest>
            {
                new() { GranteeId = serviceAccountId, Read = true, Write = true },
            },
        };

        var response = await _client.PostAsJsonAsync($"/projects/{projectId}/access-policies", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task CreateProjectAccessPolicies_MismatchedOrgIds_NotFound(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var (projectId, serviceAccountId) = await CreateProjectAndServiceAccountAsync(org.Id, true);
        await SetupProjectAndServiceAccountPermissionAsync(permissionType, projectId, serviceAccountId);

        var request = new AccessPoliciesCreateRequest
        {
            ServiceAccountAccessPolicyRequests = new List<AccessPolicyRequest>
            {
                new() { GranteeId = serviceAccountId, Read = true, Write = true },
            },
        };


        var response = await _client.PostAsJsonAsync($"/projects/{projectId}/access-policies", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task CreateProjectAccessPolicies_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var (projectId, serviceAccountId) = await CreateProjectAndServiceAccountAsync(org.Id);
        await SetupProjectAndServiceAccountPermissionAsync(permissionType, projectId, serviceAccountId);

        var request = new AccessPoliciesCreateRequest
        {
            ServiceAccountAccessPolicyRequests = new List<AccessPolicyRequest>
            {
                new() { GranteeId = serviceAccountId, Read = true, Write = true },
            },
        };

        var response = await _client.PostAsJsonAsync($"/projects/{projectId}/access-policies", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectAccessPoliciesResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(serviceAccountId, result.ServiceAccountAccessPolicies.First().ServiceAccountId);
        Assert.True(result.ServiceAccountAccessPolicies.First().Read);
        Assert.True(result.ServiceAccountAccessPolicies.First().Write);
        AssertHelper.AssertRecent(result.ServiceAccountAccessPolicies.First().RevisionDate);
        AssertHelper.AssertRecent(result.ServiceAccountAccessPolicies.First().CreationDate);

        var createdAccessPolicy =
            await _accessPolicyRepository.GetByIdAsync(result.ServiceAccountAccessPolicies.First().Id);
        Assert.NotNull(createdAccessPolicy);
        Assert.Equal(result.ServiceAccountAccessPolicies.First().Read, createdAccessPolicy.Read);
        Assert.Equal(result.ServiceAccountAccessPolicies.First().Write, createdAccessPolicy.Write);
        Assert.Equal(result.ServiceAccountAccessPolicies.First().Id, createdAccessPolicy.Id);
        AssertHelper.AssertRecent(createdAccessPolicy.CreationDate);
        AssertHelper.AssertRecent(createdAccessPolicy.RevisionDate);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task UpdateAccessPolicy_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        const bool expectedRead = true;
        const bool expectedWrite = false;
        var request = new AccessPolicyUpdateRequest { Read = expectedRead, Write = expectedWrite };

        var response = await _client.PutAsJsonAsync($"/access-policies/{initData.AccessPolicyId}", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAccessPolicy_NoPermission()
    {
        // Create a new account as a user
        await _organizationHelper.Initialize(true, true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var initData = await SetupAccessPolicyRequest(orgUser.OrganizationId);

        const bool expectedRead = true;
        const bool expectedWrite = false;
        var request = new AccessPolicyUpdateRequest { Read = expectedRead, Write = expectedWrite };

        var response = await _client.PutAsJsonAsync($"/access-policies/{initData.AccessPolicyId}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task UpdateAccessPolicy_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);
            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = initData.ProjectId, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        const bool expectedRead = true;
        const bool expectedWrite = false;
        var request = new AccessPolicyUpdateRequest { Read = expectedRead, Write = expectedWrite };

        var response = await _client.PutAsJsonAsync($"/access-policies/{initData.AccessPolicyId}", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ServiceAccountProjectAccessPolicyResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(expectedRead, result.Read);
        Assert.Equal(expectedWrite, result.Write);
        AssertHelper.AssertRecent(result.RevisionDate);

        var updatedAccessPolicy = await _accessPolicyRepository.GetByIdAsync(result.Id);
        Assert.NotNull(updatedAccessPolicy);
        Assert.Equal(expectedRead, updatedAccessPolicy.Read);
        Assert.Equal(expectedWrite, updatedAccessPolicy.Write);
        AssertHelper.AssertRecent(updatedAccessPolicy.RevisionDate);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task DeleteAccessPolicy_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        var response = await _client.DeleteAsync($"/access-policies/{initData.AccessPolicyId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccessPolicy_NoPermission()
    {
        // Create a new account as a user
        await _organizationHelper.Initialize(true, true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var initData = await SetupAccessPolicyRequest(orgUser.OrganizationId);

        var response = await _client.DeleteAsync($"/access-policies/{initData.AccessPolicyId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task DeleteAccessPolicy_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);
            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = initData.ProjectId, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var response = await _client.DeleteAsync($"/access-policies/{initData.AccessPolicyId}");
        response.EnsureSuccessStatusCode();

        var test = await _accessPolicyRepository.GetByIdAsync(initData.AccessPolicyId);
        Assert.Null(test);
    }

    [Fact]
    public async Task GetProjectAccessPolicies_ReturnsEmpty()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response = await _client.GetAsync($"/projects/{project.Id}/access-policies");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectAccessPoliciesResponseModel>();

        Assert.NotNull(result);
        Assert.Empty(result.UserAccessPolicies);
        Assert.Empty(result.GroupAccessPolicies);
        Assert.Empty(result.ServiceAccountAccessPolicies);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetProjectAccessPolicies_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var initData = await SetupAccessPolicyRequest(org.Id);

        var response = await _client.GetAsync($"/projects/{initData.ProjectId}/access-policies");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectAccessPolicies_NoPermission()
    {
        // Create a new account as a user
        await _organizationHelper.Initialize(true, true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var initData = await SetupAccessPolicyRequest(orgUser.OrganizationId);

        var response = await _client.GetAsync($"/projects/{initData.ProjectId}/access-policies");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetProjectAccessPolicies(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);
            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = initData.ProjectId, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var response = await _client.GetAsync($"/projects/{initData.ProjectId}/access-policies");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectAccessPoliciesResponseModel>();

        Assert.NotNull(result?.ServiceAccountAccessPolicies);
        Assert.Single(result.ServiceAccountAccessPolicies);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetPeoplePotentialGrantees_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var response =
            await _client.GetAsync(
                $"/organizations/{org.Id}/access-policies/people/potential-grantees");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetPeoplePotentialGrantees_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);
        }

        var response =
            await _client.GetAsync(
                $"/organizations/{org.Id}/access-policies/people/potential-grantees");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<PotentialGranteeResponseModel>>();

        Assert.NotNull(result?.Data);
        Assert.NotEmpty(result.Data);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetServiceAccountPotentialGrantees_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var response =
            await _client.GetAsync(
                $"/organizations/{org.Id}/access-policies/service-accounts/potential-grantees");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetServiceAccountPotentialGrantees_OnlyReturnsServiceAccountsWithWriteAccess()
    {
        // Create a new account as a user
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });


        var response =
            await _client.GetAsync(
                $"/organizations/{org.Id}/access-policies/service-accounts/potential-grantees");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<PotentialGranteeResponseModel>>();

        Assert.NotNull(result?.Data);
        Assert.Empty(result.Data);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetServiceAccountsPotentialGrantees_Success(PermissionType permissionType)
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

            await _accessPolicyRepository.CreateManyAsync(
                new List<BaseAccessPolicy>
                {
                    new UserServiceAccountAccessPolicy
                    {
                        GrantedServiceAccountId = serviceAccount.Id,
                        OrganizationUserId = orgUser.Id,
                        Read = true,
                        Write = true,
                    },
                });
        }

        var response =
            await _client.GetAsync(
                $"/organizations/{org.Id}/access-policies/service-accounts/potential-grantees");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<PotentialGranteeResponseModel>>();

        Assert.NotNull(result?.Data);
        Assert.NotEmpty(result.Data);
        Assert.Equal(serviceAccount.Id, result.Data.First(x => x.Id == serviceAccount.Id).Id);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetProjectPotentialGrantees_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var response =
            await _client.GetAsync(
                $"/organizations/{org.Id}/access-policies/projects/potential-grantees");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectPotentialGrantees_OnlyReturnsProjectsWithWriteAccess()
    {
        // Create a new account as a user
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        await _projectRepository.CreateAsync(new Project { OrganizationId = org.Id, Name = _mockEncryptedString });


        var response =
            await _client.GetAsync(
                $"/organizations/{org.Id}/access-policies/projects/potential-grantees");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<PotentialGranteeResponseModel>>();

        Assert.NotNull(result?.Data);
        Assert.Empty(result.Data);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetProjectPotentialGrantees_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

            await _accessPolicyRepository.CreateManyAsync(
                new List<BaseAccessPolicy>
                {
                    new UserProjectAccessPolicy
                    {
                        GrantedProjectId = project.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                    },
                });
        }

        var response =
            await _client.GetAsync(
                $"/organizations/{org.Id}/access-policies/projects/potential-grantees");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<PotentialGranteeResponseModel>>();

        Assert.NotNull(result?.Data);
        Assert.NotEmpty(result.Data);
        Assert.Equal(project.Id, result.Data.First(x => x.Id == project.Id).Id);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetProjectPeopleAccessPolicies_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString
        });

        var response = await _client.GetAsync($"/projects/{project.Id}/access-policies/people");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectPeopleAccessPolicies_ReturnsEmpty()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString
        });

        var response = await _client.GetAsync($"/projects/{project.Id}/access-policies/people");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectPeopleAccessPoliciesResponseModel>();

        Assert.NotNull(result);
        Assert.Empty(result.UserAccessPolicies);
        Assert.Empty(result.GroupAccessPolicies);
    }

    [Fact]
    public async Task GetProjectPeopleAccessPolicies_NoPermission_NotFound()
    {
        await _organizationHelper.Initialize(true, true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = orgUser.OrganizationId,
            Name = _mockEncryptedString
        });

        var response = await _client.GetAsync($"/projects/{project.Id}/access-policies/people");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetProjectPeopleAccessPolicies_Success(PermissionType permissionType)
    {
        var (_, organizationUser) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var (project, _) = await SetupProjectPeoplePermissionAsync(permissionType, organizationUser);

        var response = await _client.GetAsync($"/projects/{project.Id}/access-policies/people");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectPeopleAccessPoliciesResponseModel>();

        Assert.NotNull(result?.UserAccessPolicies);
        Assert.Single(result.UserAccessPolicies);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task PutProjectPeopleAccessPolicies_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (_, organizationUser) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var (project, request) = await SetupProjectPeopleRequestAsync(PermissionType.RunAsAdmin, organizationUser);

        var response = await _client.PutAsJsonAsync($"/projects/{project.Id}/access-policies/people", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutProjectPeopleAccessPolicies_NoPermission()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, organizationUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString
        });

        var request = new PeopleAccessPoliciesRequestModel
        {
            UserAccessPolicyRequests = new List<AccessPolicyRequest>
            {
                new() { GranteeId = organizationUser.Id, Read = true, Write = true }
            }
        };

        var response = await _client.PutAsJsonAsync($"/projects/{project.Id}/access-policies/people", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task PutProjectPeopleAccessPolicies_MismatchedOrgIds_NotFound(PermissionType permissionType)
    {
        var (_, organizationUser) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var (project, request) = await SetupProjectPeopleRequestAsync(permissionType, organizationUser);
        var newOrg = await _organizationHelper.CreateSmOrganizationAsync();
        var group = await _groupRepository.CreateAsync(new Group
        {
            OrganizationId = newOrg.Id,
            Name = _mockEncryptedString
        });
        request.GroupAccessPolicyRequests = new List<AccessPolicyRequest>
        {
            new() { GranteeId = group.Id, Read = true, Write = true }
        };

        var response = await _client.PutAsJsonAsync($"/projects/{project.Id}/access-policies/people", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task PutProjectPeopleAccessPolicies_Success(PermissionType permissionType)
    {
        var (_, organizationUser) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var (project, request) = await SetupProjectPeopleRequestAsync(permissionType, organizationUser);

        var response = await _client.PutAsJsonAsync($"/projects/{project.Id}/access-policies/people", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectPeopleAccessPoliciesResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(request.UserAccessPolicyRequests.First().GranteeId,
            result.UserAccessPolicies.First().OrganizationUserId);
        Assert.True(result.UserAccessPolicies.First().Read);
        Assert.True(result.UserAccessPolicies.First().Write);

        var createdAccessPolicy =
            await _accessPolicyRepository.GetByIdAsync(result.UserAccessPolicies.First().Id);
        Assert.NotNull(createdAccessPolicy);
        Assert.Equal(result.UserAccessPolicies.First().Read, createdAccessPolicy.Read);
        Assert.Equal(result.UserAccessPolicies.First().Write, createdAccessPolicy.Write);
        Assert.Equal(result.UserAccessPolicies.First().Id, createdAccessPolicy.Id);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetServiceAccountPeopleAccessPolicies_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);
        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response = await _client.GetAsync($"/service-accounts/{serviceAccount.Id}/access-policies/people");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetServiceAccountPeopleAccessPolicies_ReturnsEmpty()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response = await _client.GetAsync($"/service-accounts/{serviceAccount.Id}/access-policies/people");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ServiceAccountPeopleAccessPoliciesResponseModel>();

        Assert.NotNull(result);
        Assert.Empty(result.UserAccessPolicies);
        Assert.Empty(result.GroupAccessPolicies);
    }

    [Fact]
    public async Task GetServiceAccountPeopleAccessPolicies_NoPermission()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response = await _client.GetAsync($"/service-accounts/{serviceAccount.Id}/access-policies/people");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetServiceAccountPeopleAccessPolicies_Success(PermissionType permissionType)
    {
        var (_, organizationUser) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var (serviceAccount, _) = await SetupServiceAccountPeoplePermissionAsync(permissionType, organizationUser);

        var response = await _client.GetAsync($"/service-accounts/{serviceAccount.Id}/access-policies/people");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ServiceAccountPeopleAccessPoliciesResponseModel>();

        Assert.NotNull(result?.UserAccessPolicies);
        Assert.Single(result.UserAccessPolicies);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task PutServiceAccountPeopleAccessPolicies_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (_, organizationUser) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var (serviceAccount, request) = await SetupServiceAccountPeopleRequestAsync(PermissionType.RunAsAdmin, organizationUser);

        var response = await _client.PutAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-policies/people", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutServiceAccountPeopleAccessPolicies_NoPermission()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, organizationUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });


        var request = new PeopleAccessPoliciesRequestModel
        {
            UserAccessPolicyRequests = new List<AccessPolicyRequest>
            {
                new() { GranteeId = organizationUser.Id, Read = true, Write = true }
            }
        };

        var response = await _client.PutAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-policies/people", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task PutServiceAccountPeopleAccessPolicies_MismatchedOrgIds_NotFound(PermissionType permissionType)
    {
        var (_, organizationUser) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var (serviceAccount, request) = await SetupServiceAccountPeopleRequestAsync(permissionType, organizationUser);
        var newOrg = await _organizationHelper.CreateSmOrganizationAsync();
        var group = await _groupRepository.CreateAsync(new Group
        {
            OrganizationId = newOrg.Id,
            Name = _mockEncryptedString
        });
        request.GroupAccessPolicyRequests = new List<AccessPolicyRequest>
        {
            new() { GranteeId = group.Id, Read = true, Write = true }
        };

        var response = await _client.PutAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-policies/people", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task PutServiceAccountPeopleAccessPolicies_Success(PermissionType permissionType)
    {
        var (_, organizationUser) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var (serviceAccount, request) = await SetupServiceAccountPeopleRequestAsync(permissionType, organizationUser);

        var response = await _client.PutAsJsonAsync($"/service-accounts/{serviceAccount.Id}/access-policies/people", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ServiceAccountPeopleAccessPoliciesResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(request.UserAccessPolicyRequests.First().GranteeId,
            result.UserAccessPolicies.First().OrganizationUserId);
        Assert.True(result.UserAccessPolicies.First().Read);
        Assert.True(result.UserAccessPolicies.First().Write);

        var createdAccessPolicy =
            await _accessPolicyRepository.GetByIdAsync(result.UserAccessPolicies.First().Id);
        Assert.NotNull(createdAccessPolicy);
        Assert.Equal(result.UserAccessPolicies.First().Read, createdAccessPolicy.Read);
        Assert.Equal(result.UserAccessPolicies.First().Write, createdAccessPolicy.Write);
        Assert.Equal(result.UserAccessPolicies.First().Id, createdAccessPolicy.Id);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetServiceAccountGrantedPoliciesAsync_SmAccessDenied_ReturnsNotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        var response = await _client.GetAsync($"/service-accounts/{initData.ServiceAccountId}/granted-policies");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetServiceAccountGrantedPoliciesAsync_NoAccessPolicies_ReturnsEmpty()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response = await _client.GetAsync($"/service-accounts/{serviceAccount.Id}/granted-policies");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ServiceAccountGrantedPoliciesPermissionDetailsResponseModel>();

        Assert.NotNull(result);
        Assert.Empty(result.GrantedProjectPolicies);
    }

    [Fact]
    public async Task GetServiceAccountGrantedPoliciesAsync_UserDoesntHavePermission_ReturnsNotFound()
    {
        // Create a new account as a user
        await _organizationHelper.Initialize(true, true, true);
        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var initData = await SetupAccessPolicyRequest(orgUser.OrganizationId);

        var response = await _client.GetAsync($"/service-accounts/{initData.ServiceAccountId}/granted-policies");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetServiceAccountGrantedPoliciesAsync_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);
        var initData = await SetupAccessPolicyRequest(org.Id);

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);
            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserServiceAccountAccessPolicy
                {
                    GrantedServiceAccountId = initData.ServiceAccountId, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                }
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var response = await _client.GetAsync($"/service-accounts/{initData.ServiceAccountId}/granted-policies");
        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<ServiceAccountGrantedPoliciesPermissionDetailsResponseModel>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.GrantedProjectPolicies);
        Assert.Equal(initData.ServiceAccountId, result.GrantedProjectPolicies.First().AccessPolicy.ServiceAccountId);
        Assert.NotNull(result.GrantedProjectPolicies.First().AccessPolicy.ServiceAccountName);
        Assert.NotNull(result.GrantedProjectPolicies.First().AccessPolicy.GrantedProjectName);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task PutServiceAccountGrantedPoliciesAsync_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (_, organizationUser) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var (serviceAccount, request) = await SetupServiceAccountGrantedPoliciesRequestAsync(PermissionType.RunAsAdmin, organizationUser, false);

        var response = await _client.PutAsJsonAsync($"/service-accounts/{serviceAccount.Id}/granted-policies", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutServiceAccountGrantedPoliciesAsync_UserHasNoPermission_ReturnsNotFound()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var (projectId, serviceAccountId) = await CreateProjectAndServiceAccountAsync(org.Id);

        var request = new ServiceAccountGrantedPoliciesRequestModel
        {
            ProjectGrantedPolicyRequests = new List<GrantedAccessPolicyRequest>
            {
                new() { GrantedId = projectId, Read = true, Write = true }
            }
        };

        var response = await _client.PutAsJsonAsync($"/service-accounts/{serviceAccountId}/granted-policies", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task PutServiceAccountGrantedPoliciesAsync_MismatchedOrgIds_ReturnsNotFound(PermissionType permissionType)
    {
        var (_, organizationUser) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var (serviceAccount, request) = await SetupServiceAccountGrantedPoliciesRequestAsync(permissionType, organizationUser, false);
        var newOrg = await _organizationHelper.CreateSmOrganizationAsync();

        var project = await _projectRepository.CreateAsync(new Project
        {
            Name = _mockEncryptedString,
            OrganizationId = newOrg.Id
        });
        request.ProjectGrantedPolicyRequests = new List<GrantedAccessPolicyRequest>
        {
            new() { GrantedId = project.Id, Read = true, Write = true }
        };

        var response = await _client.PutAsJsonAsync($"/service-accounts/{serviceAccount.Id}/granted-policies", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin, false)]
    [InlineData(PermissionType.RunAsAdmin, true)]
    [InlineData(PermissionType.RunAsUserWithPermission, false)]
    [InlineData(PermissionType.RunAsUserWithPermission, true)]
    public async Task PutServiceAccountGrantedPoliciesAsync_Success(PermissionType permissionType, bool createPreviousAccessPolicy)
    {
        var (_, organizationUser) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var (serviceAccount, request) = await SetupServiceAccountGrantedPoliciesRequestAsync(permissionType, organizationUser, createPreviousAccessPolicy);

        var response = await _client.PutAsJsonAsync($"/service-accounts/{serviceAccount.Id}/granted-policies", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ServiceAccountGrantedPoliciesPermissionDetailsResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(request.ProjectGrantedPolicyRequests.First().GrantedId,
            result.GrantedProjectPolicies.First().AccessPolicy.GrantedProjectId);
        Assert.True(result.GrantedProjectPolicies.First().AccessPolicy.Read);
        Assert.True(result.GrantedProjectPolicies.First().AccessPolicy.Write);
        Assert.True(result.GrantedProjectPolicies.First().HasPermission);
        Assert.Single(result.GrantedProjectPolicies);
    }

    private async Task<RequestSetupData> SetupAccessPolicyRequest(Guid organizationId)
    {
        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = organizationId,
            Name = _mockEncryptedString,
        });

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = organizationId,
            Name = _mockEncryptedString,
        });

        var accessPolicy = await _accessPolicyRepository.CreateManyAsync(
            new List<BaseAccessPolicy>
            {
                new ServiceAccountProjectAccessPolicy
                {
                    Read = true, Write = true, ServiceAccountId = serviceAccount.Id, GrantedProjectId = project.Id,
                },
            });

        return new RequestSetupData
        {
            ProjectId = project.Id,
            ServiceAccountId = serviceAccount.Id,
            AccessPolicyId = accessPolicy.First().Id,
        };
    }

    private async Task<(Project project, OrganizationUser currentUser)> SetupProjectPeoplePermissionAsync(
        PermissionType permissionType,
        OrganizationUser organizationUser)
    {
        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = organizationUser.OrganizationId,
            Name = _mockEncryptedString
        });

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);
            organizationUser = orgUser;
        }

        var accessPolicies = new List<BaseAccessPolicy>
        {
            new UserProjectAccessPolicy
            {
                GrantedProjectId = project.Id,
                OrganizationUserId = organizationUser.Id,
                Read = true,
                Write = true
            }
        };
        await _accessPolicyRepository.CreateManyAsync(accessPolicies);

        return (project, organizationUser);
    }

    private async Task<(ServiceAccount serviceAccount, OrganizationUser currentUser)> SetupServiceAccountPeoplePermissionAsync(
        PermissionType permissionType,
        OrganizationUser organizationUser)
    {
        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = organizationUser.OrganizationId,
            Name = _mockEncryptedString,
        });

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);
            organizationUser = orgUser;
        }

        var accessPolicies = new List<BaseAccessPolicy>
        {
            new UserServiceAccountAccessPolicy
            {
                GrantedServiceAccountId = serviceAccount.Id,
                OrganizationUserId = organizationUser.Id,
                Read = true,
                Write = true
            }
        };

        await _accessPolicyRepository.CreateManyAsync(accessPolicies);

        return (serviceAccount, organizationUser);
    }

    private async Task<(Project project, PeopleAccessPoliciesRequestModel request)> SetupProjectPeopleRequestAsync(
        PermissionType permissionType, OrganizationUser organizationUser)
    {
        var (project, currentUser) = await SetupProjectPeoplePermissionAsync(permissionType, organizationUser);
        var request = new PeopleAccessPoliciesRequestModel
        {
            UserAccessPolicyRequests = new List<AccessPolicyRequest>
            {
                new() { GranteeId = currentUser.Id, Read = true, Write = true }
            }
        };
        return (project, request);
    }

    private async Task<(ServiceAccount serviceAccount, PeopleAccessPoliciesRequestModel request)> SetupServiceAccountPeopleRequestAsync(
        PermissionType permissionType, OrganizationUser organizationUser)
    {
        var (serviceAccount, currentUser) = await SetupServiceAccountPeoplePermissionAsync(permissionType, organizationUser);
        var request = new PeopleAccessPoliciesRequestModel
        {
            UserAccessPolicyRequests = new List<AccessPolicyRequest>
            {
                new() { GranteeId = currentUser.Id, Read = true, Write = true }
            }
        };
        return (serviceAccount, request);
    }

    private async Task<(Guid ProjectId, Guid ServiceAccountId)> CreateProjectAndServiceAccountAsync(Guid organizationId,
        bool misMatchOrganization = false)
    {
        var newOrg = new Organization();
        if (misMatchOrganization)
        {
            newOrg = await _organizationHelper.CreateSmOrganizationAsync();
        }

        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = misMatchOrganization ? newOrg.Id : organizationId,
            Name = _mockEncryptedString,
        });

        var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
        {
            OrganizationId = organizationId,
            Name = _mockEncryptedString,
        });

        return (project.Id, serviceAccount.Id);
    }

    private async Task SetupProjectAndServiceAccountPermissionAsync(PermissionType permissionType, Guid projectId,
        Guid serviceAccountId)
    {
        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);
            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = projectId, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                },
                new UserServiceAccountAccessPolicy
                {
                    GrantedServiceAccountId = serviceAccountId,
                    OrganizationUserId = orgUser.Id,
                    Read = true,
                    Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }
    }

    private async Task<(ServiceAccount serviceAccount, ServiceAccountGrantedPoliciesRequestModel request)> SetupServiceAccountGrantedPoliciesRequestAsync(
        PermissionType permissionType, OrganizationUser organizationUser, bool createPreviousAccessPolicy)
    {
        var (serviceAccount, currentUser) = await SetupServiceAccountPeoplePermissionAsync(permissionType, organizationUser);
        var project = await _projectRepository.CreateAsync(new Project
        {
            Name = _mockEncryptedString,
            OrganizationId = organizationUser.OrganizationId
        });
        var accessPolicies = new List<BaseAccessPolicy>
        {
            new UserProjectAccessPolicy
            {
                GrantedProjectId = project.Id, OrganizationUserId = currentUser.Id, Read = true, Write = true,
            },
        };

        if (createPreviousAccessPolicy)
        {
            var anotherProject = await _projectRepository.CreateAsync(new Project
            {
                Name = _mockEncryptedString,
                OrganizationId = organizationUser.OrganizationId
            });

            accessPolicies.Add(new UserProjectAccessPolicy
            {
                GrantedProjectId = anotherProject.Id,
                OrganizationUserId = currentUser.Id,
                Read = true,
                Write = true,
            });
            accessPolicies.Add(new ServiceAccountProjectAccessPolicy
            {
                GrantedProjectId = anotherProject.Id,
                ServiceAccountId = serviceAccount.Id,
                Read = true,
                Write = true,
            });
        }

        await _accessPolicyRepository.CreateManyAsync(accessPolicies);

        var request = new ServiceAccountGrantedPoliciesRequestModel
        {
            ProjectGrantedPolicyRequests = new List<GrantedAccessPolicyRequest>
            {
                new() { GrantedId = project.Id, Read = true, Write = true }
            }
        };
        return (serviceAccount, request);
    }

    private class RequestSetupData
    {
        public Guid ProjectId { get; set; }
        public Guid AccessPolicyId { get; set; }
        public Guid ServiceAccountId { get; set; }
    }
}

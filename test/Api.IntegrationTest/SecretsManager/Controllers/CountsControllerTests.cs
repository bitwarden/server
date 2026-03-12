using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Enums;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

public class CountsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretRepository _secretRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly LoginHelper _loginHelper;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;


    public CountsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _projectRepository = _factory.GetService<IProjectRepository>();
        _secretRepository = _factory.GetService<ISecretRepository>();
        _serviceAccountRepository = _factory.GetService<IServiceAccountRepository>();
        _apiKeyRepository = _factory.GetService<IApiKeyRepository>();
        _accessPolicyRepository = _factory.GetService<IAccessPolicyRepository>();
        _groupRepository = _factory.GetService<IGroupRepository>();
        _organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
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
    public async Task GetByOrganizationAsync_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets,
        bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var response = await _client.GetAsync($"/organizations/{org.Id}/sm-counts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByOrganizationAsync_RunAsServiceAccount_NotFound()
    {
        var (_, org, _) = await SetupProjectsWithAccessAsync(PermissionType.RunAsServiceAccountWithPermission);

        var response = await _client.GetAsync($"/organizations/{org.Id}/sm-counts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByOrganizationAsync_UserWithoutPermission_ZeroCounts()
    {
        var (_, org, _) = await SetupProjectsWithAccessAsync(PermissionType.RunAsUserWithPermission, 0);

        var projects = await CreateProjectsAsync(org.Id);
        await CreateSecretsAsync(org.Id, projects[0]);
        await CreateServiceAccountsAsync(org.Id);

        var response = await _client.GetAsync($"/organizations/{org.Id}/sm-counts");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OrganizationCountsResponseModel>();
        Assert.NotNull(result);
        Assert.Equal(0, result.Projects);
        Assert.Equal(0, result.Secrets);
        Assert.Equal(0, result.ServiceAccounts);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetByOrganizationAsync_Success(PermissionType permissionType)
    {
        var (projects, org, user) = await SetupProjectsWithAccessAsync(permissionType);
        var projectsWithoutAccess = await CreateProjectsAsync(org.Id);

        var secrets = await CreateSecretsAsync(org.Id, projects[0]);
        var secretsWithoutAccess = await CreateSecretsAsync(org.Id, projectsWithoutAccess[0]);
        var secretsWithoutProject = await CreateSecretsAsync(org.Id, null);

        var serviceAccounts = await CreateServiceAccountsAsync(org.Id);
        await CreateUserServiceAccountAccessPolicyAsync(user.Id, serviceAccounts[0].Id);

        var response = await _client.GetAsync($"/organizations/{org.Id}/sm-counts");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OrganizationCountsResponseModel>();
        Assert.NotNull(result);
        if (permissionType == PermissionType.RunAsAdmin)
        {
            Assert.Equal(projects.Count + projectsWithoutAccess.Count, result.Projects);
            Assert.Equal(secrets.Count + secretsWithoutAccess.Count + secretsWithoutProject.Count,
                result.Secrets);
            Assert.Equal(serviceAccounts.Count, result.ServiceAccounts);
        }
        else
        {
            Assert.Equal(projects.Count, result.Projects);
            Assert.Equal(secrets.Count, result.Secrets);
            Assert.Equal(1, result.ServiceAccounts);
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
    public async Task GetByProjectAsync_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets,
        bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var projects = await CreateProjectsAsync(org.Id);

        var response = await _client.GetAsync($"/projects/{projects[0].Id}/sm-counts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByProjectAsync_RunAsServiceAccount_NotFound()
    {
        var (projects, _, _) = await SetupProjectsWithAccessAsync(PermissionType.RunAsServiceAccountWithPermission);

        var response = await _client.GetAsync($"/projects/{projects[0].Id}/sm-counts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetByProjectAsync_NonExistingProject_NotFound(PermissionType permissionType)
    {
        await SetupProjectsWithAccessAsync(permissionType);

        var response = await _client.GetAsync($"/projects/{Guid.NewGuid().ToString()}/sm-counts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByProjectAsync_UserWithoutPermission_ZeroCounts()
    {
        var (_, org, user) = await SetupProjectsWithAccessAsync(PermissionType.RunAsUserWithPermission, 0);

        var projects = await CreateProjectsAsync(org.Id);

        await CreateSecretsAsync(org.Id, projects[0]);

        var groups = await CreateGroupsAsync(org.Id, user);
        await CreateGroupProjectAccessPolicyAsync(groups[0].Id, projects[0].Id);

        var serviceAccounts = await CreateServiceAccountsAsync(org.Id);
        await CreateServiceAccountProjectAccessPolicyAsync(projects[0].Id, serviceAccounts[0].Id);

        var response = await _client.GetAsync($"/projects/{projects[0].Id}/sm-counts");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectCountsResponseModel>();
        Assert.NotNull(result);
        Assert.Equal(0, result.Secrets);
        Assert.Equal(0, result.People);
        Assert.Equal(0, result.ServiceAccounts);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin, true)]
    [InlineData(PermissionType.RunAsUserWithPermission, false)]
    [InlineData(PermissionType.RunAsUserWithPermission, true)]
    public async Task GetByProjectAsync_Success(PermissionType permissionType, bool userProjectWriteAccess)
    {
        var (projects, org, user) = await SetupProjectsWithAccessAsync(permissionType, 3, userProjectWriteAccess);

        var secrets = await CreateSecretsAsync(org.Id, projects[0]);
        await CreateSecretsAsync(org.Id, projects[1]);

        var groups = await CreateGroupsAsync(org.Id, user);
        await CreateGroupProjectAccessPolicyAsync(groups[0].Id, projects[0].Id);
        await CreateGroupProjectAccessPolicyAsync(groups[0].Id, projects[1].Id);
        var (_, user2) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await CreateUserProjectAccessPolicyAsync(user2.Id, projects[0].Id);

        var serviceAccounts = await CreateServiceAccountsAsync(org.Id);
        await CreateUserServiceAccountAccessPolicyAsync(user.Id, serviceAccounts[0].Id);
        await CreateServiceAccountProjectAccessPolicyAsync(projects[0].Id, serviceAccounts[0].Id);

        var response = await _client.GetAsync($"/projects/{projects[0].Id}/sm-counts");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectCountsResponseModel>();
        Assert.NotNull(result);
        Assert.Equal(secrets.Count, result.Secrets);
        if (userProjectWriteAccess)
        {
            Assert.Equal(permissionType == PermissionType.RunAsAdmin ? 2 : 3, result.People);
            Assert.Equal(1, result.ServiceAccounts);
        }
        else
        {
            Assert.Equal(0, result.People);
            Assert.Equal(0, result.ServiceAccounts);
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
    public async Task GetByServiceAccountAsync_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets,
        bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var serviceAccounts = await CreateServiceAccountsAsync(org.Id);

        var response = await _client.GetAsync($"/service-accounts/{serviceAccounts[0].Id}/sm-counts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByServiceAccountAsync_RunAsServiceAccount_NotFound()
    {
        var (_, org, _) = await SetupProjectsWithAccessAsync(PermissionType.RunAsServiceAccountWithPermission);

        var serviceAccounts = await CreateServiceAccountsAsync(org.Id);

        var response = await _client.GetAsync($"/service-accounts/{serviceAccounts[0].Id}/sm-counts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetByServiceAccountAsync_NonExistingServiceAccount_NotFound(PermissionType permissionType)
    {
        await SetupProjectsWithAccessAsync(permissionType);

        var response = await _client.GetAsync($"/service-accounts/{Guid.NewGuid().ToString()}/sm-counts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByServiceAccountAsync_UserWithoutPermission_ZeroCounts()
    {
        var (_, org, user) = await SetupProjectsWithAccessAsync(PermissionType.RunAsUserWithPermission, 0);

        var projects = await CreateProjectsAsync(org.Id);

        var serviceAccounts = await CreateServiceAccountsAsync(org.Id);
        await CreateServiceAccountProjectAccessPolicyAsync(projects[0].Id, serviceAccounts[0].Id);

        var groups = await CreateGroupsAsync(org.Id, user);
        await CreateGroupServiceAccountAccessPolicyAsync(groups[0].Id, serviceAccounts[0].Id);

        await CreateApiKeysAsync(serviceAccounts[0]);

        var response = await _client.GetAsync($"/service-accounts/{serviceAccounts[0].Id}/sm-counts");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ServiceAccountCountsResponseModel>();
        Assert.NotNull(result);
        Assert.Equal(0, result.Projects);
        Assert.Equal(0, result.People);
        Assert.Equal(0, result.AccessTokens);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetByServiceAccountAsync_Success(PermissionType permissionType)
    {
        var (projects, org, user) = await SetupProjectsWithAccessAsync(permissionType);

        var serviceAccounts = await CreateServiceAccountsAsync(org.Id);
        await CreateServiceAccountProjectAccessPolicyAsync(projects[0].Id, serviceAccounts[0].Id);
        await CreateServiceAccountProjectAccessPolicyAsync(projects[0].Id, serviceAccounts[1].Id);
        await CreateServiceAccountProjectAccessPolicyAsync(projects[1].Id, serviceAccounts[0].Id);

        await CreateUserServiceAccountAccessPolicyAsync(user.Id, serviceAccounts[0].Id);
        var groups = await CreateGroupsAsync(org.Id, user);
        await CreateGroupServiceAccountAccessPolicyAsync(groups[0].Id, serviceAccounts[0].Id);
        await CreateGroupServiceAccountAccessPolicyAsync(groups[0].Id, serviceAccounts[1].Id);
        var (_, user2) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await CreateUserServiceAccountAccessPolicyAsync(user2.Id, serviceAccounts[0].Id);

        var apiKeys = await CreateApiKeysAsync(serviceAccounts[0]);
        await CreateApiKeysAsync(serviceAccounts[1]);

        var response = await _client.GetAsync($"/service-accounts/{serviceAccounts[0].Id}/sm-counts");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ServiceAccountCountsResponseModel>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Projects);
        Assert.Equal(3, result.People);
        Assert.Equal(apiKeys.Count, result.AccessTokens);
    }

    private async Task<List<Project>> CreateProjectsAsync(Guid orgId, int numberToCreate = 3)
    {
        var projects = new List<Project>();
        for (var i = 0; i < numberToCreate; i++)
        {
            var project = await _projectRepository.CreateAsync(new Project
            {
                OrganizationId = orgId,
                Name = _mockEncryptedString,
            });
            projects.Add(project);
        }

        return projects;
    }

    private async Task<List<Secret>> CreateSecretsAsync(Guid organizationId, Project? project, int numberToCreate = 3)
    {
        var secrets = new List<Secret>();
        for (var i = 0; i < numberToCreate; i++)
        {
            var secret = await _secretRepository.CreateAsync(new Secret
            {
                OrganizationId = organizationId,
                Key = _mockEncryptedString,
                Value = _mockEncryptedString,
                Note = _mockEncryptedString,
                Projects = project != null ? new List<Project> { project } : null
            });
            secrets.Add(secret);
        }

        return secrets;
    }

    private async Task<List<ServiceAccount>> CreateServiceAccountsAsync(Guid organizationId, int numberToCreate = 3)
    {
        var serviceAccounts = new List<ServiceAccount>();
        for (var i = 0; i < numberToCreate; i++)
        {
            var serviceAccount = await _serviceAccountRepository.CreateAsync(new ServiceAccount
            {
                OrganizationId = organizationId,
                Name = _mockEncryptedString
            });
            serviceAccounts.Add(serviceAccount);
        }

        return serviceAccounts;
    }

    private async Task<List<Group>> CreateGroupsAsync(Guid organizationId, OrganizationUser? user,
        int numberToCreate = 3)
    {
        var groups = new List<Group>();

        for (var i = 0; i < numberToCreate; i++)
        {
            var group = await _groupRepository.CreateAsync(new Group
            {
                OrganizationId = organizationId,
                Name = _mockEncryptedString,
            });
            groups.Add(group);

            if (user != null)
            {
                await _organizationUserRepository.UpdateGroupsAsync(user.Id, [group.Id]);
            }
        }

        return groups;
    }

    private async Task<List<ApiKey>> CreateApiKeysAsync(ServiceAccount serviceAccount, int numberToCreate = 3)
    {
        var apiKeys = new List<ApiKey>();

        for (var i = 0; i < numberToCreate; i++)
        {
            var apiKey = await _apiKeyRepository.CreateAsync(new ApiKey
            {
                Name = _mockEncryptedString,
                ServiceAccountId = serviceAccount.Id,
                Scope = "api.secrets",
                Key = serviceAccount.OrganizationId.ToString(),
                EncryptedPayload = _mockEncryptedString,
                ClientSecretHash = "807613bbf6692e6809a571bc694a4719a5aa6863f7a62bd714003ab73de588e6"
            });
            apiKeys.Add(apiKey);
        }

        return apiKeys;
    }

    private async Task<(List<Project>, Organization, OrganizationUser)> SetupProjectsWithAccessAsync(
        PermissionType permissionType,
        int projectsToCreate = 3,
        bool writeAccess = false)
    {
        var (org, owner) = await _organizationHelper.Initialize(true, true, true);
        var projects = await CreateProjectsAsync(org.Id, projectsToCreate);
        var user = owner;

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                await _loginHelper.LoginAsync(_email);
                break;
            case PermissionType.RunAsUserWithPermission:
                {
                    var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
                    user = orgUser;
                    await _loginHelper.LoginAsync(email);

                    foreach (var project in projects)
                    {
                        await CreateUserProjectAccessPolicyAsync(user.Id, project.Id, writeAccess);
                    }

                    break;
                }
            case PermissionType.RunAsServiceAccountWithPermission:
                {
                    var apiKeyDetails = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
                    await _loginHelper.LoginWithApiKeyAsync(apiKeyDetails);

                    foreach (var project in projects)
                    {
                        await CreateServiceAccountProjectAccessPolicyAsync(project.Id, apiKeyDetails.ApiKey.ServiceAccountId!.Value);
                    }

                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null);
        }

        return (projects, org, user);
    }

    private async Task CreateUserProjectAccessPolicyAsync(Guid userId, Guid projectId, bool write = false)
    {
        var policy = new UserProjectAccessPolicy
        {
            OrganizationUserId = userId,
            GrantedProjectId = projectId,
            Read = true,
            Write = write,
        };
        await _accessPolicyRepository.CreateManyAsync([policy]);
    }

    private async Task CreateGroupProjectAccessPolicyAsync(Guid groupId, Guid projectId)
    {
        var policy = new GroupProjectAccessPolicy
        {
            GroupId = groupId,
            GrantedProjectId = projectId,
            Read = true,
            Write = false,
        };
        await _accessPolicyRepository.CreateManyAsync([policy]);
    }


    private async Task CreateUserServiceAccountAccessPolicyAsync(Guid userId, Guid serviceAccountId)
    {
        var policy = new UserServiceAccountAccessPolicy
        {
            OrganizationUserId = userId,
            GrantedServiceAccountId = serviceAccountId,
            Read = true,
            Write = false,
        };
        await _accessPolicyRepository.CreateManyAsync([policy]);
    }

    private async Task CreateGroupServiceAccountAccessPolicyAsync(Guid groupId, Guid serviceAccountId)
    {
        var policy = new GroupServiceAccountAccessPolicy
        {
            GroupId = groupId,
            GrantedServiceAccountId = serviceAccountId,
            Read = true,
            Write = false
        };
        await _accessPolicyRepository.CreateManyAsync([policy]);
    }

    private async Task CreateServiceAccountProjectAccessPolicyAsync(Guid projectId, Guid serviceAccountId)
    {
        var policy = new ServiceAccountProjectAccessPolicy
        {
            ServiceAccountId = serviceAccountId,
            GrantedProjectId = projectId,
            Read = true,
            Write = false,
        };
        await _accessPolicyRepository.CreateManyAsync([policy]);
    }
}

using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Enums;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
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
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly LoginHelper _loginHelper;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;


    public CountsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _projectRepository = _factory.GetService<IProjectRepository>();
        _accessPolicyRepository = _factory.GetService<IAccessPolicyRepository>();
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
    public async Task GetByOrganizationAsync_ServiceAccountAccess_NotFound()
    {
        var (_, org) = await SetupProjectsWithAccessAsync(PermissionType.RunAsServiceAccountWithPermission);

        var response = await _client.GetAsync($"/organizations/{org.Id}/sm-counts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByOrganizationAsync_UserWithoutPermission_EmptyList()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        await CreateProjectsAsync(org.Id);

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
        var (projectIds, org) = await SetupProjectsWithAccessAsync(permissionType);
        var projectsIdsWithoutAccess = await CreateProjectsAsync(org.Id);

        var response = await _client.GetAsync($"/organizations/{org.Id}/sm-counts");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OrganizationCountsResponseModel>();
        Assert.NotNull(result);
        if (permissionType == PermissionType.RunAsAdmin)
        {
            Assert.Equal(projectIds.Count + projectsIdsWithoutAccess.Count, result.Projects);
        }
        else
        {
            Assert.Equal(projectIds.Count, result.Projects);
        }

        // TODO
        // Assert.Equal(?, result.ServiceAccounts);
    }

    private async Task<List<Guid>> CreateProjectsAsync(Guid orgId, int numberToCreate = 3)
    {
        var projectIds = new List<Guid>();
        for (var i = 0; i < numberToCreate; i++)
        {
            var project = await _projectRepository.CreateAsync(new Project
            {
                OrganizationId = orgId,
                Name = _mockEncryptedString,
            });
            projectIds.Add(project.Id);
        }

        return projectIds;
    }

    private async Task<(List<Guid>, Organization)> SetupProjectsWithAccessAsync(PermissionType permissionType,
        int projectsToCreate = 3)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var projectIds = await CreateProjectsAsync(org.Id, projectsToCreate);

        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                await _loginHelper.LoginAsync(_email);
                break;
            case PermissionType.RunAsUserWithPermission:
                {
                    var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
                    await _loginHelper.LoginAsync(email);

                    var accessPolicies = projectIds.Select(projectId => new UserProjectAccessPolicy
                    {
                        GrantedProjectId = projectId,
                        OrganizationUserId = orgUser.Id,
                        Read = true,
                        Write = true,
                    })
                        .Cast<BaseAccessPolicy>()
                        .ToList();
                    await _accessPolicyRepository.CreateManyAsync(accessPolicies);
                    break;
                }
            case PermissionType.RunAsServiceAccountWithPermission:
                {
                    var apiKeyDetails = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
                    await _loginHelper.LoginWithApiKeyAsync(apiKeyDetails);

                    var accessPolicies = projectIds.Select(projectId => new ServiceAccountProjectAccessPolicy
                    {
                        GrantedProjectId = projectId,
                        ServiceAccountId = apiKeyDetails.ApiKey.ServiceAccountId,
                        Read = true,
                        Write = true,
                    })
                        .Cast<BaseAccessPolicy>()
                        .ToList();
                    await _accessPolicyRepository.CreateManyAsync(accessPolicies);
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null);
        }

        return (projectIds, org);
    }
}

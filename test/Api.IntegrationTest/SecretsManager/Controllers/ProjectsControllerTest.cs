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

public class ProjectsControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly IProjectRepository _projectRepository;
    private readonly IAccessPolicyRepository _accessPolicyRepository;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public ProjectsControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _projectRepository = _factory.GetService<IProjectRepository>();
        _accessPolicyRepository = _factory.GetService<IAccessPolicyRepository>();
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

        var response = await _client.GetAsync($"/organizations/{org.Id}/projects");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListByOrganization_UserWithoutPermission_EmptyList()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        await CreateProjectsAsync(org.Id);

        var response = await _client.GetAsync($"/organizations/{org.Id}/projects");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<ProjectResponseModel>>();
        Assert.NotNull(result);
        Assert.Empty(result!.Data);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task ListByOrganization_Success(PermissionType permissionType)
    {
        var (projectIds, org) = await SetupProjectsWithAccessAsync(permissionType);

        var response = await _client.GetAsync($"/organizations/{org.Id}/projects");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<ProjectResponseModel>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.Data);
        Assert.Equal(projectIds.Count, result.Data.Count());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Create_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);

        var request = new ProjectCreateRequestModel { Name = _mockEncryptedString };

        var response = await _client.PostAsJsonAsync($"/organizations/{org.Id}/projects", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task Create_Success(PermissionType permissionType)
    {
        var (org, adminOrgUser) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);
        var orgUserId = adminOrgUser.Id;
        var currentUserId = adminOrgUser.UserId!.Value;

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await LoginAsync(email);
            orgUserId = orgUser.Id;
            currentUserId = orgUser.UserId!.Value;
        }

        var request = new ProjectCreateRequestModel { Name = _mockEncryptedString };

        var response = await _client.PostAsJsonAsync($"/organizations/{org.Id}/projects", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProjectResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(request.Name, result!.Name);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);

        var createdProject = await _projectRepository.GetByIdAsync(new Guid(result.Id));
        Assert.NotNull(result);
        Assert.Equal(request.Name, createdProject.Name);
        AssertHelper.AssertRecent(createdProject.RevisionDate);
        AssertHelper.AssertRecent(createdProject.CreationDate);
        Assert.Null(createdProject.DeletedDate);

        // Check permissions have been bootstrapped.
        var accessPolicies = await _accessPolicyRepository.GetManyByGrantedProjectIdAsync(createdProject.Id, currentUserId);
        Assert.NotNull(accessPolicies);
        var ap = (UserProjectAccessPolicy)accessPolicies.First();
        Assert.Equal(createdProject.Id, ap.GrantedProjectId);
        Assert.Equal(orgUserId, ap.OrganizationUserId);
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

        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var mockEncryptedString2 =
            "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";
        var request = new ProjectCreateRequestModel { Name = mockEncryptedString2 };

        var response = await _client.PutAsJsonAsync($"/projects/{initialProject.Id}", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task Update_Success(PermissionType permissionType)
    {
        var initialProject = await SetupProjectWithAccessAsync(permissionType);

        var mockEncryptedString2 =
            "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

        var request = new ProjectUpdateRequestModel { Name = mockEncryptedString2 };

        var response = await _client.PutAsJsonAsync($"/projects/{initialProject.Id}", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProjectResponseModel>();
        Assert.NotEqual(initialProject.Name, result!.Name);
        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.NotEqual(initialProject.RevisionDate, result.RevisionDate);

        var updatedProject = await _projectRepository.GetByIdAsync(new Guid(result.Id));
        Assert.NotNull(result);
        Assert.Equal(request.Name, updatedProject.Name);
        AssertHelper.AssertRecent(updatedProject.RevisionDate);
        Assert.Null(updatedProject.DeletedDate);
        Assert.NotEqual(initialProject.Name, updatedProject.Name);
        Assert.NotEqual(initialProject.RevisionDate, updatedProject.RevisionDate);
    }

    [Fact]
    public async Task Update_NonExistingProject_NotFound()
    {
        await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);

        var request = new ProjectUpdateRequestModel
        {
            Name =
                "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=",
        };

        var response = await _client.PutAsJsonAsync("/projects/c53de509-4581-402c-8cbd-f26d2c516fba", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_MissingAccessPolicy_NotFound()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var request = new ProjectUpdateRequestModel
        {
            Name =
                "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=",
        };

        var response = await _client.PutAsJsonAsync($"/projects/{project.Id}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Get_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);

        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var mockEncryptedString2 =
            "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";
        var request = new ProjectCreateRequestModel { Name = mockEncryptedString2 };

        var response = await _client.PutAsJsonAsync($"/projects/{project.Id}", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_MissingAccessPolicy_NotFound()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var createdProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response = await _client.GetAsync($"/projects/{createdProject.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task Get_Success(PermissionType permissionType)
    {
        var project = await SetupProjectWithAccessAsync(permissionType);

        var response = await _client.GetAsync($"/projects/{project.Id}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProjectPermissionDetailsResponseModel>();
        Assert.Equal(project.Name, result!.Name);
        Assert.Equal(project.RevisionDate, result.RevisionDate);
        Assert.Equal(project.CreationDate, result.CreationDate);
        Assert.True(result.Read);
        Assert.True(result.Write);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Delete_SmNotEnabled_NotFound(bool useSecrets, bool accessSecrets)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets);
        await LoginAsync(_email);

        var projectIds = await CreateProjectsAsync(org.Id);

        var response = await _client.PostAsync("/projects/delete", JsonContent.Create(projectIds));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_MissingAccessPolicy_AccessDenied()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

        var projectIds = await CreateProjectsAsync(org.Id);

        var response = await _client.PostAsync("/projects/delete", JsonContent.Create(projectIds));

        var results = await response.Content.ReadFromJsonAsync<ListResponseModel<BulkDeleteResponseModel>>();
        Assert.NotNull(results);
        Assert.Equal(projectIds.OrderBy(x => x),
            results!.Data.Select(x => x.Id).OrderBy(x => x));
        Assert.All(results.Data, item => Assert.Equal("access denied", item.Error));
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task Delete_Success(PermissionType permissionType)
    {
        var (projectIds, _) = await SetupProjectsWithAccessAsync(permissionType);

        var response = await _client.PostAsync("/projects/delete", JsonContent.Create(projectIds));
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<ListResponseModel<BulkDeleteResponseModel>>();
        Assert.NotNull(results);
        Assert.Equal(projectIds.OrderBy(x => x),
            results!.Data.Select(x => x.Id).OrderBy(x => x));
        Assert.DoesNotContain(results.Data, x => x.Error != null);

        var projects = await _projectRepository.GetManyWithSecretsByIds(projectIds);
        Assert.Empty(projects);
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
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);
        var projectIds = await CreateProjectsAsync(org.Id, projectsToCreate);

        if (permissionType == PermissionType.RunAsAdmin)
        {
            return (projectIds, org);
        }

        var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await LoginAsync(email);

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

        return (projectIds, org);
    }

    private async Task<Project> SetupProjectWithAccessAsync(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true);
        await LoginAsync(_email);

        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        if (permissionType == PermissionType.RunAsAdmin)
        {
            return initialProject;
        }

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

        return initialProject;
    }
}

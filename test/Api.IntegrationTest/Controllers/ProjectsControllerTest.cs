using System.Net;
using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Api.SecretManagerFeatures.Models.Response;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class ProjectsControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly IProjectRepository _projectRepository;
    private Organization _organization = null!;

    public ProjectsControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _projectRepository = _factory.GetService<IProjectRepository>();
    }

    public async Task InitializeAsync()
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);
        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, ownerEmail: ownerEmail, billingEmail: ownerEmail);
        var tokens = await _factory.LoginAsync(ownerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
    }

    public async Task<OrganizationUser> LoginAsNewOrgUser(OrganizationUserType type = OrganizationUserType.User)
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        var orgUser = await OrganizationTestHelpers.CreateUserAsync(_factory, _organization.Id, email, type);
        var tokens = await _factory.LoginAsync(email);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);

        return orgUser;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateProject_Success()
    {
        var request = new ProjectCreateRequestModel { Name = _mockEncryptedString };

        var response = await _client.PostAsJsonAsync($"/organizations/{_organization.Id}/projects", request);
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
    }

    [Fact]
    public async Task CreateProject_NoPermission()
    {
        var request = new ProjectCreateRequestModel { Name = _mockEncryptedString };

        var response = await _client.PostAsJsonAsync("/organizations/911d9106-7cf1-4d55-a3f9-f9abdeadecb3/projects", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_Success()
    {
        var initialProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = _organization.Id,
            Name = _mockEncryptedString
        });

        var mockEncryptedString2 = "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

        var request = new ProjectUpdateRequestModel()
        {
            Name = mockEncryptedString2
        };

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
        AssertHelper.AssertRecent(updatedProject.CreationDate);
        Assert.Null(updatedProject.DeletedDate);
        Assert.NotEqual(initialProject.Name, updatedProject.Name);
        Assert.NotEqual(initialProject.RevisionDate, updatedProject.RevisionDate);
    }

    [Fact]
    public async Task UpdateProject_NotFound()
    {
        var request = new ProjectUpdateRequestModel()
        {
            Name = "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=",
        };

        var response = await _client.PutAsJsonAsync("/projects/c53de509-4581-402c-8cbd-f26d2c516fba", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_MissingPermission()
    {
        // Create a new account as a user
        await LoginAsNewOrgUser();

        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = _organization.Id,
            Name = _mockEncryptedString
        });


        var request = new ProjectUpdateRequestModel()
        {
            Name = "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=",
        };

        var response = await _client.PutAsJsonAsync($"/projects/{project.Id}", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProject()
    {
        var createdProject = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = _organization.Id,
            Name = _mockEncryptedString
        });

        var response = await _client.GetAsync($"/projects/{createdProject.Id}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProjectResponseModel>();
        Assert.Equal(createdProject.Name, result!.Name);
        Assert.Equal(createdProject.RevisionDate, result.RevisionDate);
        Assert.Equal(createdProject.CreationDate, result.CreationDate);
    }

    [Fact]
    public async Task GetProjectsByOrganization()
    {
        var projectsToCreate = 3;
        var projectIds = new List<Guid>();
        for (var i = 0; i < projectsToCreate; i++)
        {
            var project = await _projectRepository.CreateAsync(new Project
            {
                OrganizationId = _organization.Id,
                Name = _mockEncryptedString
            });
            projectIds.Add(project.Id);
        }

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/projects");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<ProjectResponseModel>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.Data);
        Assert.Equal(projectIds.Count, result.Data.Count());
    }

    [Fact]
    public async Task DeleteProjects()
    {
        var projectsToDelete = 3;
        var projectIds = new List<Guid>();
        for (var i = 0; i < projectsToDelete; i++)
        {
            var project = await _projectRepository.CreateAsync(new Project
            {
                OrganizationId = _organization.Id,
                Name = _mockEncryptedString,
            });
            projectIds.Add(project.Id);
        }

        var response = await _client.PostAsync("/projects/delete", JsonContent.Create(projectIds));
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<ListResponseModel<BulkDeleteResponseModel>>();

        Assert.NotNull(results);

        var index = 0;
        foreach (var result in results!.Data)
        {
            Assert.Equal(projectIds[index], result.Id);
            Assert.Null(result.Error);
            index++;
        }

        var projects = await _projectRepository.GetManyByIds(projectIds);
        Assert.Empty(projects);
    }
}

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

public class ProjectsControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly IProjectRepository _projectRepository;
    private Organization? _organization;

    public ProjectsControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _projectRepository = _factory.GetService<IProjectRepository>();
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
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateProject()
    {
        var request = new ProjectCreateRequestModel()
        {
            Name = _mockEncryptedString
        };

        var response = await _client.PostAsJsonAsync($"/organizations/{_organization.Id}/projects", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Project>();

        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);
        Assert.Null(result.DeletedDate);

        var createdProject = await _projectRepository.GetByIdAsync(result.Id);
        Assert.NotNull(result);
        Assert.Equal(request.Name, createdProject.Name);
        AssertHelper.AssertRecent(createdProject.RevisionDate);
        AssertHelper.AssertRecent(createdProject.CreationDate);
        Assert.Null(createdProject.DeletedDate);
    }

    [Fact]
    public async Task UpdateProject()
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
        var result = await response.Content.ReadFromJsonAsync<Project>();
        Assert.NotEqual(initialProject.Name, result.Name);
        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.NotEqual(initialProject.RevisionDate, result.RevisionDate);
        Assert.Null(result.DeletedDate);

        var updatedProject = await _projectRepository.GetByIdAsync(result.Id);
        Assert.NotNull(result);
        Assert.Equal(request.Name, updatedProject.Name);
        AssertHelper.AssertRecent(updatedProject.RevisionDate);
        AssertHelper.AssertRecent(updatedProject.CreationDate);
        Assert.Null(updatedProject.DeletedDate);
        Assert.NotEqual(initialProject.Name, updatedProject.Name);
        Assert.NotEqual(initialProject.RevisionDate, updatedProject.RevisionDate);
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
        var result = await response.Content.ReadFromJsonAsync<Project>();
        Assert.Equal(createdProject.Name, result.Name);
        Assert.Equal(createdProject.RevisionDate, result.RevisionDate);
        Assert.Equal(createdProject.CreationDate, result.CreationDate);
        Assert.Null(result.DeletedDate);
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
        var content = await response.Content.ReadAsStringAsync();

        var jsonResult = JsonDocument.Parse(content);

        Assert.NotEmpty(jsonResult.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(projectIds.Count(), jsonResult.RootElement.GetProperty("data").EnumerateArray().Count());
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

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);

        var jsonResult = JsonDocument.Parse(content);
        var index = 0;
        foreach (var element in jsonResult.RootElement.GetProperty("data").EnumerateArray())
        {
            Assert.Equal(projectIds[index].ToString(), element.GetProperty("id").ToString());
            Assert.Empty(element.GetProperty("error").ToString());
            index++;
        }

        var projects = await _projectRepository.GetManyByIds(projectIds);
        Assert.Empty(projects);
    }
}

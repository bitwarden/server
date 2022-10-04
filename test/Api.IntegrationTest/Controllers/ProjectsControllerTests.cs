using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.SecretManagerFeatures.Models.Request;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class ProjectsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly IProjectRepository _projectRepository;
    private Organization? _organization;

    public ProjectsControllerTests(ApiApplicationFactory factory)
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
}

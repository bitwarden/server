using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Entities;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

public class SecretsControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;
    private Organization _organization = null!;

    public SecretsControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _secretRepository = _factory.GetService<ISecretRepository>();
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
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateSecret()
    {
        var request = new SecretCreateRequestModel()
        {
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        };

        var response = await _client.PostAsJsonAsync($"/organizations/{_organization.Id}/secrets", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SecretResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(request.Key, result!.Key);
        Assert.Equal(request.Value, result.Value);
        Assert.Equal(request.Note, result.Note);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);

        var createdSecret = await _secretRepository.GetByIdAsync(new Guid(result.Id));
        Assert.NotNull(result);
        Assert.Equal(request.Key, createdSecret.Key);
        Assert.Equal(request.Value, createdSecret.Value);
        Assert.Equal(request.Note, createdSecret.Note);
        AssertHelper.AssertRecent(createdSecret.RevisionDate);
        AssertHelper.AssertRecent(createdSecret.CreationDate);
        Assert.Null(createdSecret.DeletedDate);
    }

    [Fact]
    public async Task CreateSecretWithProject()
    {
        var project = await _projectRepository.CreateAsync(new Project()
        {
            Id = new Guid(),
            OrganizationId = _organization.Id,
            Name = _mockEncryptedString
        });
        var projectIds = new[] { project.Id };
        var secretRequest = new SecretCreateRequestModel()
        {
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString,
            ProjectIds = projectIds,
        };
        var secretResponse = await _client.PostAsJsonAsync($"/organizations/{_organization.Id}/secrets", secretRequest);
        secretResponse.EnsureSuccessStatusCode();
        var secretResult = await secretResponse.Content.ReadFromJsonAsync<SecretResponseModel>();

        var secret = (await _secretRepository.GetManyByProjectIdAsync(project.Id)).First();

        Assert.NotNull(secretResult);
        Assert.Equal(secret.Id.ToString(), secretResult!.Id);
        Assert.Equal(secret.OrganizationId.ToString(), secretResult.OrganizationId);
        Assert.Equal(secret.Key, secretResult.Key);
        Assert.Equal(secret.Value, secretResult.Value);
        Assert.Equal(secret.Note, secretResult.Note);
        Assert.Equal(secret.CreationDate, secretResult.CreationDate);
        Assert.Equal(secret.RevisionDate, secretResult.RevisionDate);
    }

    [Fact]
    public async Task UpdateSecret()
    {
        var initialSecret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = _organization.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        var request = new SecretUpdateRequestModel()
        {
            Key = _mockEncryptedString,
            Value = "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=",
            Note = _mockEncryptedString
        };

        var response = await _client.PutAsJsonAsync($"/secrets/{initialSecret.Id}", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SecretResponseModel>();
        Assert.Equal(request.Key, result!.Key);
        Assert.Equal(request.Value, result.Value);
        Assert.NotEqual(initialSecret.Value, result.Value);
        Assert.Equal(request.Note, result.Note);
        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.NotEqual(initialSecret.RevisionDate, result.RevisionDate);

        var updatedSecret = await _secretRepository.GetByIdAsync(new Guid(result.Id));
        Assert.NotNull(result);
        Assert.Equal(request.Key, updatedSecret.Key);
        Assert.Equal(request.Value, updatedSecret.Value);
        Assert.Equal(request.Note, updatedSecret.Note);
        AssertHelper.AssertRecent(updatedSecret.RevisionDate);
        AssertHelper.AssertRecent(updatedSecret.CreationDate);
        Assert.Null(updatedSecret.DeletedDate);
        Assert.NotEqual(initialSecret.Value, updatedSecret.Value);
        Assert.NotEqual(initialSecret.RevisionDate, updatedSecret.RevisionDate);
    }

    [Fact]
    public async Task DeleteSecrets()
    {
        var secretsToDelete = 3;
        var secretIds = new List<Guid>();
        for (var i = 0; i < secretsToDelete; i++)
        {
            var secret = await _secretRepository.CreateAsync(new Secret
            {
                OrganizationId = _organization.Id,
                Key = _mockEncryptedString,
                Value = _mockEncryptedString,
                Note = _mockEncryptedString
            });
            secretIds.Add(secret.Id);
        }

        var response = await _client.PostAsync("/secrets/delete", JsonContent.Create(secretIds));
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<ListResponseModel<BulkDeleteResponseModel>>();
        Assert.NotNull(results);

        var index = 0;
        foreach (var result in results!.Data)
        {
            Assert.Equal(secretIds[index], result.Id);
            Assert.Null(result.Error);
            index++;
        }

        var secrets = await _secretRepository.GetManyByIds(secretIds);
        Assert.Empty(secrets);
    }

    [Fact]
    public async Task GetSecret()
    {
        var createdSecret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = _organization.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });


        var response = await _client.GetAsync($"/secrets/{createdSecret.Id}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SecretResponseModel>();
        Assert.Equal(createdSecret.Key, result!.Key);
        Assert.Equal(createdSecret.Value, result.Value);
        Assert.Equal(createdSecret.Note, result.Note);
        Assert.Equal(createdSecret.RevisionDate, result.RevisionDate);
        Assert.Equal(createdSecret.CreationDate, result.CreationDate);
    }

    [Fact]
    public async Task GetSecretsByOrganization()
    {
        var secretsToCreate = 3;
        var secretIds = new List<Guid>();
        for (var i = 0; i < secretsToCreate; i++)
        {
            var secret = await _secretRepository.CreateAsync(new Secret
            {
                OrganizationId = _organization.Id,
                Key = _mockEncryptedString,
                Value = _mockEncryptedString,
                Note = _mockEncryptedString
            });
            secretIds.Add(secret.Id);
        }

        var response = await _client.GetAsync($"/organizations/{_organization.Id}/secrets");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SecretWithProjectsListResponseModel>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.Secrets);
        Assert.Equal(secretIds.Count, result.Secrets.Count());
    }
}

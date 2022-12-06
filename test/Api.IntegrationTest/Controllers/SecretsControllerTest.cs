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

// TODO Quartz jobs are conflicting when integration tests are ran in parallel. 
// For now sequently run integration tests.
[Collection("Sequential")]
public class SecretsControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;
    private Organization? _organization;

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
        var result = await response.Content.ReadFromJsonAsync<Secret>();

        Assert.NotNull(result);
        Assert.Equal(request.Key, result.Key);
        Assert.Equal(request.Value, result.Value);
        Assert.Equal(request.Note, result.Note);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);
        Assert.Null(result.DeletedDate);

        var createdSecret = await _secretRepository.GetByIdAsync(result.Id);
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

        var secretRequest = new SecretCreateRequestModel()
        {
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString,
            ProjectId = project.Id,
        };
        var secretResponse = await _client.PostAsJsonAsync($"/organizations/{_organization.Id}/secrets", secretRequest);
        secretResponse.EnsureSuccessStatusCode();
        var secretResult = await secretResponse.Content.ReadFromJsonAsync<Secret>();

        List<Secret> secrets = (await _secretRepository.GetManyByProjectIdAsync(project.Id)).ToList();

        Assert.NotNull(secretResult);
        Assert.Equal(secrets.First().Id, secretResult.Id);
        Assert.Equal(secrets.First().OrganizationId, secretResult.OrganizationId);
        Assert.Equal(secrets.First().Key, secretResult.Key);
        Assert.Equal(secrets.First().Value, secretResult.Value);
        Assert.Equal(secrets.First().Note, secretResult.Note);
        Assert.Equal(secrets.First().CreationDate, secretResult.CreationDate);
        Assert.Equal(secrets.First().RevisionDate, secretResult.RevisionDate);
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
        var result = await response.Content.ReadFromJsonAsync<Secret>();
        Assert.Equal(request.Key, result.Key);
        Assert.Equal(request.Value, result.Value);
        Assert.NotEqual(initialSecret.Value, result.Value);
        Assert.Equal(request.Note, result.Note);
        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.NotEqual(initialSecret.RevisionDate, result.RevisionDate);
        Assert.Null(result.DeletedDate);

        var updatedSecret = await _secretRepository.GetByIdAsync(result.Id);
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

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);

        var jsonResult = JsonDocument.Parse(content);
        var index = 0;
        foreach (var element in jsonResult.RootElement.GetProperty("data").EnumerateArray())
        {
            Assert.Equal(secretIds[index].ToString(), element.GetProperty("id").ToString());
            Assert.Empty(element.GetProperty("error").ToString());
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
        var result = await response.Content.ReadFromJsonAsync<Secret>();
        Assert.Equal(createdSecret.Key, result.Key);
        Assert.Equal(createdSecret.Value, result.Value);
        Assert.Equal(createdSecret.Note, result.Note);
        Assert.Equal(createdSecret.RevisionDate, result.RevisionDate);
        Assert.Equal(createdSecret.CreationDate, result.CreationDate);
        Assert.Null(result.DeletedDate);
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
        var content = await response.Content.ReadAsStringAsync();

        var jsonResult = JsonDocument.Parse(content);

        Assert.NotEmpty(jsonResult.RootElement.GetProperty("secrets").EnumerateArray());
        Assert.Equal(secretIds.Count(), jsonResult.RootElement.GetProperty("secrets").EnumerateArray().Count());
    }
}

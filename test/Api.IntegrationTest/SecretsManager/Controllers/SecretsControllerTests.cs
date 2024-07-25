using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Enums;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
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

public class SecretsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly ISecretRepository _secretRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly LoginHelper _loginHelper;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public SecretsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _secretRepository = _factory.GetService<ISecretRepository>();
        _projectRepository = _factory.GetService<IProjectRepository>();
        _accessPolicyRepository = _factory.GetService<IAccessPolicyRepository>();
        _serviceAccountRepository = _factory.GetService<IServiceAccountRepository>();
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
    public async Task ListByOrganization_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var response = await _client.GetAsync($"/organizations/{org.Id}/secrets");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task ListByOrganization_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var project = await _projectRepository.CreateAsync(new Project
        {
            Id = new Guid(),
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = project.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var secretIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var secret = await _secretRepository.CreateAsync(new Secret
            {
                OrganizationId = org.Id,
                Key = _mockEncryptedString,
                Value = _mockEncryptedString,
                Note = _mockEncryptedString,
                Projects = new List<Project> { project }

            });
            secretIds.Add(secret.Id);
        }

        var response = await _client.GetAsync($"/organizations/{org.Id}/secrets");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SecretWithProjectsListResponseModel>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Secrets);
        Assert.Equal(secretIds.Count, result.Secrets.Count());
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Create_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var request = new SecretCreateRequestModel
        {
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        };

        var response = await _client.PostAsJsonAsync($"/organizations/{org.Id}/secrets", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_WithoutProject_RunAsAdmin_Success(bool withAccessPolicies)
    {
        var (organizationUser, request) = await SetupSecretCreateRequestAsync(withAccessPolicies);

        var response = await _client.PostAsJsonAsync($"/organizations/{organizationUser.OrganizationId}/secrets", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SecretResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(request.Key, result.Key);
        Assert.Equal(request.Value, result.Value);
        Assert.Equal(request.Note, result.Note);
        AssertHelper.AssertRecent(result.RevisionDate);
        AssertHelper.AssertRecent(result.CreationDate);

        var createdSecret = await _secretRepository.GetByIdAsync(result.Id);
        Assert.NotNull(result);
        Assert.Equal(request.Key, createdSecret.Key);
        Assert.Equal(request.Value, createdSecret.Value);
        Assert.Equal(request.Note, createdSecret.Note);
        AssertHelper.AssertRecent(createdSecret.RevisionDate);
        AssertHelper.AssertRecent(createdSecret.CreationDate);
        Assert.Null(createdSecret.DeletedDate);

        if (withAccessPolicies)
        {
            var secretAccessPolicies = await _accessPolicyRepository.GetSecretAccessPoliciesAsync(result.Id, organizationUser.UserId!.Value);
            Assert.NotNull(secretAccessPolicies);
            Assert.NotEmpty(secretAccessPolicies.UserAccessPolicies);
            Assert.Equal(organizationUser.Id, secretAccessPolicies.UserAccessPolicies.First().OrganizationUserId);
            Assert.Equal(result.Id, secretAccessPolicies.UserAccessPolicies.First().GrantedSecretId);
            Assert.True(secretAccessPolicies.UserAccessPolicies.First().Read);
            Assert.True(secretAccessPolicies.UserAccessPolicies.First().Write);
        }
    }

    [Fact]
    public async Task CreateWithDifferentProjectOrgId_RunAsAdmin_NotFound()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);
        var anotherOrg = await _organizationHelper.CreateSmOrganizationAsync();

        var project =
            await _projectRepository.CreateAsync(new Project { Name = "123", OrganizationId = anotherOrg.Id });

        var request = new SecretCreateRequestModel
        {
            ProjectIds = new[] { project.Id },
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        };

        var response = await _client.PostAsJsonAsync($"/organizations/{org.Id}/secrets", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateWithMultipleProjects_RunAsAdmin_BadRequest()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var projectA = await _projectRepository.CreateAsync(new Project { OrganizationId = org.Id, Name = "123A" });
        var projectB = await _projectRepository.CreateAsync(new Project { OrganizationId = org.Id, Name = "123B" });

        var request = new SecretCreateRequestModel
        {
            ProjectIds = new Guid[] { projectA.Id, projectB.Id },
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString,
        };

        var response = await _client.PostAsJsonAsync($"/organizations/{org.Id}/secrets", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateWithoutProject_RunAsUser_NotFound()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var request = new SecretCreateRequestModel
        {
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        };

        var response = await _client.PostAsJsonAsync($"/organizations/{org.Id}/secrets", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_RunAsServiceAccount_WithAccessPolicies_NotFound()
    {
        var (organizationUser, secretRequest) =
            await SetupSecretWithProjectCreateRequestAsync(PermissionType.RunAsServiceAccountWithPermission, true);

        var response =
            await _client.PostAsJsonAsync($"/organizations/{organizationUser.OrganizationId}/secrets", secretRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin, false)]
    [InlineData(PermissionType.RunAsAdmin, true)]
    [InlineData(PermissionType.RunAsUserWithPermission, false)]
    [InlineData(PermissionType.RunAsUserWithPermission, true)]
    [InlineData(PermissionType.RunAsServiceAccountWithPermission, false)]
    public async Task Create_WithProject_Success(PermissionType permissionType, bool withAccessPolicies)
    {
        var (organizationUser, secretRequest) = await SetupSecretWithProjectCreateRequestAsync(permissionType, withAccessPolicies);

        var secretResponse = await _client.PostAsJsonAsync($"/organizations/{organizationUser.OrganizationId}/secrets", secretRequest);
        secretResponse.EnsureSuccessStatusCode();
        var result = await secretResponse.Content.ReadFromJsonAsync<SecretResponseModel>();

        Assert.NotNull(result);
        var secret = await _secretRepository.GetByIdAsync(result.Id);
        Assert.Equal(secret.Id, result.Id);
        Assert.Equal(secret.OrganizationId, result.OrganizationId);
        Assert.Equal(secret.Key, result.Key);
        Assert.Equal(secret.Value, result.Value);
        Assert.Equal(secret.Note, result.Note);
        Assert.Equal(secret.CreationDate, result.CreationDate);
        Assert.Equal(secret.RevisionDate, result.RevisionDate);

        if (withAccessPolicies)
        {
            var secretAccessPolicies = await _accessPolicyRepository.GetSecretAccessPoliciesAsync(secret.Id, organizationUser.UserId!.Value);
            Assert.NotNull(secretAccessPolicies);
            Assert.NotEmpty(secretAccessPolicies.UserAccessPolicies);
            Assert.Equal(organizationUser.Id, secretAccessPolicies.UserAccessPolicies.First().OrganizationUserId);
            Assert.Equal(secret.Id, secretAccessPolicies.UserAccessPolicies.First().GrantedSecretId);
            Assert.True(secretAccessPolicies.UserAccessPolicies.First().Read);
            Assert.True(secretAccessPolicies.UserAccessPolicies.First().Write);
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
    public async Task Get_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        var response = await _client.GetAsync($"/organizations/secrets/{secret.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task Get_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var project = await _projectRepository.CreateAsync(new Project()
        {
            Id = new Guid(),
            OrganizationId = org.Id,
            Name = _mockEncryptedString
        });

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = project.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }
        else
        {
            var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.Admin, true);
            await _loginHelper.LoginAsync(email);
        }

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString,
            Projects = new List<Project> { project }
        });

        var response = await _client.GetAsync($"/secrets/{secret.Id}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SecretResponseModel>();
        Assert.Equal(secret.Key, result!.Key);
        Assert.Equal(secret.Value, result.Value);
        Assert.Equal(secret.Note, result.Note);
        Assert.Equal(secret.RevisionDate, result.RevisionDate);
        Assert.Equal(secret.CreationDate, result.CreationDate);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetSecretsByProject_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);
        await _loginHelper.LoginAsync(_email);

        var project = await _projectRepository.CreateAsync(new Project
        {
            OrganizationId = org.Id,
            Name = _mockEncryptedString,
        });

        var response = await _client.GetAsync($"/projects/{project.Id}/secrets");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSecretsByProject_UserWithNoPermission_EmptyList()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var project = await _projectRepository.CreateAsync(new Project()
        {
            Id = new Guid(),
            OrganizationId = org.Id,
            Name = _mockEncryptedString
        });

        await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString,
            Projects = new List<Project> { project },
        });

        var response = await _client.GetAsync($"/projects/{project.Id}/secrets");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SecretWithProjectsListResponseModel>();
        Assert.NotNull(result);
        Assert.Empty(result.Secrets);
        Assert.Empty(result.Projects);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetSecretsByProject_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var project = await _projectRepository.CreateAsync(new Project()
        {
            Id = new Guid(),
            OrganizationId = org.Id,
            Name = _mockEncryptedString
        });

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = project.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString,
            Projects = new List<Project> { project },
        });

        var response = await _client.GetAsync($"/projects/{project.Id}/secrets");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SecretWithProjectsListResponseModel>();
        Assert.NotEmpty(result!.Secrets);
        Assert.Equal(secret.Id, result.Secrets.First().Id);
        Assert.Equal(secret.OrganizationId, result.Secrets.First().OrganizationId);
        Assert.Equal(secret.Key, result.Secrets.First().Key);
        Assert.Equal(secret.CreationDate, result.Secrets.First().CreationDate);
        Assert.Equal(secret.RevisionDate, result.Secrets.First().RevisionDate);
        Assert.Equal(secret.Projects!.First().Id, result.Projects.First().Id);
        Assert.Equal(secret.Projects!.First().Name, result.Projects.First().Name);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Update_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        var request = new SecretUpdateRequestModel
        {
            Key = _mockEncryptedString,
            Value = "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=",
            Note = _mockEncryptedString
        };

        var response = await _client.PutAsJsonAsync($"/organizations/secrets/{secret.Id}", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsServiceAccountWithPermission, true)]
    public async Task Update_RunAsServiceAccountWithAccessPolicyUpdate_NotFound(PermissionType permissionType, bool withAccessPolices)
    {
        var (secret, request) = await SetupSecretUpdateRequestAsync(permissionType, withAccessPolices);

        var response = await _client.PutAsJsonAsync($"/secrets/{secret.Id}", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin, false)]
    [InlineData(PermissionType.RunAsAdmin, true)]
    [InlineData(PermissionType.RunAsUserWithPermission, false)]
    [InlineData(PermissionType.RunAsUserWithPermission, true)]
    [InlineData(PermissionType.RunAsServiceAccountWithPermission, false)]
    public async Task Update_Success(PermissionType permissionType, bool withAccessPolices)
    {
        var (secret, request) = await SetupSecretUpdateRequestAsync(permissionType, withAccessPolices);

        var response = await _client.PutAsJsonAsync($"/secrets/{secret.Id}", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SecretResponseModel>();
        Assert.Equal(request.Key, result!.Key);
        Assert.Equal(request.Value, result.Value);
        Assert.NotEqual(secret.Value, result.Value);
        Assert.Equal(request.Note, result.Note);
        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.NotEqual(secret.RevisionDate, result.RevisionDate);

        var updatedSecret = await _secretRepository.GetByIdAsync(result.Id);
        Assert.NotNull(result);
        Assert.Equal(request.Key, updatedSecret.Key);
        Assert.Equal(request.Value, updatedSecret.Value);
        Assert.Equal(request.Note, updatedSecret.Note);
        AssertHelper.AssertRecent(updatedSecret.RevisionDate);
        AssertHelper.AssertRecent(updatedSecret.CreationDate);
        Assert.Null(updatedSecret.DeletedDate);
        Assert.NotEqual(secret.Value, updatedSecret.Value);
        Assert.NotEqual(secret.RevisionDate, updatedSecret.RevisionDate);

        if (withAccessPolices)
        {
            var secretAccessPolicies = await _accessPolicyRepository.GetSecretAccessPoliciesAsync(secret.Id,
                request.AccessPoliciesRequests.UserAccessPolicyRequests.First().GranteeId);
            Assert.NotNull(secretAccessPolicies);
            Assert.NotEmpty(secretAccessPolicies.UserAccessPolicies);
            Assert.Equal(request.AccessPoliciesRequests.UserAccessPolicyRequests.First().GranteeId,
                secretAccessPolicies.UserAccessPolicies.First().OrganizationUserId);
            Assert.Equal(secret.Id, secretAccessPolicies.UserAccessPolicies.First().GrantedSecretId);
            Assert.True(secretAccessPolicies.UserAccessPolicies.First().Read);
            Assert.True(secretAccessPolicies.UserAccessPolicies.First().Write);
        }
    }

    [Fact]
    public async Task UpdateWithDifferentProjectOrgId_RunAsAdmin_NotFound()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);
        var anotherOrg = await _organizationHelper.CreateSmOrganizationAsync();

        var project = await _projectRepository.CreateAsync(new Project { Name = "123", OrganizationId = anotherOrg.Id });

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        var request = new SecretUpdateRequestModel
        {
            Key = _mockEncryptedString,
            Value = "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=",
            Note = _mockEncryptedString,
            ProjectIds = new Guid[] { project.Id },
        };

        var response = await _client.PutAsJsonAsync($"/secrets/{secret.Id}", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateWithMultipleProjects_BadRequest()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var projectA = await _projectRepository.CreateAsync(new Project { OrganizationId = org.Id, Name = "123A" });
        var projectB = await _projectRepository.CreateAsync(new Project { OrganizationId = org.Id, Name = "123B" });

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        var request = new SecretUpdateRequestModel
        {
            Key = _mockEncryptedString,
            Value = "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=",
            Note = _mockEncryptedString,
            ProjectIds = new Guid[] { projectA.Id, projectB.Id },
        };

        var response = await _client.PutAsJsonAsync($"/secrets/{secret.Id}", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Delete_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });
        var secretIds = new[] { secret.Id };

        var response = await _client.PostAsJsonAsync($"/secrets/delete", secretIds);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_MissingAccessPolicy_AccessDenied()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
        await _loginHelper.LoginAsync(email);

        var (_, secretIds) = await CreateSecretsAsync(org.Id);

        var response = await _client.PostAsync("/secrets/delete", JsonContent.Create(secretIds));

        var results = await response.Content.ReadFromJsonAsync<ListResponseModel<BulkDeleteResponseModel>>();
        Assert.NotNull(results);
        Assert.Equal(secretIds.OrderBy(x => x),
            results.Data.Select(x => x.Id).OrderBy(x => x));
        Assert.All(results.Data, item => Assert.Equal("access denied", item.Error));
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    [InlineData(PermissionType.RunAsServiceAccountWithPermission)]
    public async Task Delete_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var (project, secretIds) = await CreateSecretsAsync(org.Id);

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = project.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true
                }
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var response = await _client.PostAsJsonAsync("/secrets/delete", secretIds);
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<ListResponseModel<BulkDeleteResponseModel>>();
        Assert.NotNull(results?.Data);
        Assert.Equal(secretIds.Count, results.Data.Count());
        foreach (var result in results.Data)
        {
            Assert.Contains(result.Id, secretIds);
            Assert.Null(result.Error);
        }

        var secrets = await _secretRepository.GetManyByIds(secretIds);
        Assert.Empty(secrets);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetSecretsByIds_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString,
        });

        var request = new GetSecretsRequestModel { Ids = new[] { secret.Id } };

        var response = await _client.PostAsJsonAsync("/secrets/get-by-ids", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSecretsByIds_SecretsNotInTheSameOrganization_NotFound()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);
        var otherOrg = await _organizationHelper.CreateSmOrganizationAsync();
        var (_, secretIds) = await CreateSecretsAsync(org.Id);
        var (_, diffOrgSecrets) = await CreateSecretsAsync(otherOrg.Id, 1);
        secretIds.AddRange(diffOrgSecrets);

        var request = new GetSecretsRequestModel { Ids = secretIds };

        var response = await _client.PostAsJsonAsync("/secrets/get-by-ids", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetSecretsByIds_SecretsNonExistent_NotFound(bool partial)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);
        var ids = new List<Guid>();

        if (partial)
        {
            var (_, secretIds) = await CreateSecretsAsync(org.Id);
            ids = secretIds;
            ids.Add(Guid.NewGuid());
        }

        var request = new GetSecretsRequestModel { Ids = ids };

        var response = await _client.PostAsJsonAsync("/secrets/get-by-ids", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task GetSecretsByIds_NoAccess_NotFound(bool runAsServiceAccount, bool partialAccess)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);

        var request = await SetupNoAccessRequestAsync(org.Id, runAsServiceAccount, partialAccess);

        var response = await _client.PostAsJsonAsync("/secrets/get-by-ids", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    [InlineData(PermissionType.RunAsServiceAccountWithPermission)]
    public async Task GetSecretsByIds_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);
        var request = await SetupGetSecretsByIdsRequestAsync(org.Id, permissionType);

        var response = await _client.PostAsJsonAsync("/secrets/get-by-ids", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<BaseSecretResponseModel>>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Data);
        Assert.Equal(request.Ids.Count(), result.Data.Count());
        Assert.All(result.Data, data => Assert.Equal(_mockEncryptedString, data.Value));
        Assert.All(result.Data, data => Assert.Equal(_mockEncryptedString, data.Key));
        Assert.All(result.Data, data => Assert.Equal(_mockEncryptedString, data.Note));
        Assert.All(result.Data, data => Assert.Equal(org.Id, data.OrganizationId));
    }


    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetSecretsByIds_DuplicateIds_BadRequest(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var (project, secretIds) = await CreateSecretsAsync(org.Id);

        secretIds.Add(secretIds[0]);

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = project.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                },
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }
        else
        {
            var (email, _) = await _organizationHelper.CreateNewUser(OrganizationUserType.Admin, true);
            await _loginHelper.LoginAsync(email);
        }

        var request = new GetSecretsRequestModel { Ids = secretIds };
        var response = await _client.PostAsJsonAsync("/secrets/get-by-ids", request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest);
        Assert.Contains("The following GUIDs were duplicated", content);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetSecretsSyncAsync_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets,
        bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var response = await _client.GetAsync($"/organizations/{org.Id}/secrets/sync");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSecretsSyncAsync_UserClient_BadRequest()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var response = await _client.GetAsync($"/organizations/{org.Id}/secrets/sync");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSecretsSyncAsync_NoSecrets_ReturnsEmptyList(bool useLastSyncedDate)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var apiKeyDetails = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
        await _loginHelper.LoginWithApiKeyAsync(apiKeyDetails);

        var requestUrl = $"/organizations/{org.Id}/secrets/sync";
        if (useLastSyncedDate)
        {
            requestUrl = $"/organizations/{org.Id}/secrets/sync?lastSyncedDate={DateTime.UtcNow.AddDays(-1)}";
        }

        var response = await _client.GetAsync(requestUrl);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SecretsSyncResponseModel>();

        Assert.NotNull(result);
        Assert.True(result.HasChanges);
        Assert.NotNull(result.Secrets);
        Assert.Empty(result.Secrets.Data);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSecretsSyncAsync_HasSecrets_ReturnsAll(bool useLastSyncedDate)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var apiKeyDetails = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
        await _loginHelper.LoginWithApiKeyAsync(apiKeyDetails);
        var secretIds = await SetupSecretsSyncRequestAsync(org.Id, apiKeyDetails.ApiKey.ServiceAccountId!.Value);

        var requestUrl = $"/organizations/{org.Id}/secrets/sync";
        if (useLastSyncedDate)
        {
            requestUrl = $"/organizations/{org.Id}/secrets/sync?lastSyncedDate={DateTime.UtcNow.AddDays(-1)}";
        }

        var response = await _client.GetAsync(requestUrl);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SecretsSyncResponseModel>();

        Assert.NotNull(result);
        Assert.True(result.HasChanges);
        Assert.NotNull(result.Secrets);
        Assert.NotEmpty(result.Secrets.Data);
        Assert.Equal(secretIds.Count, result.Secrets.Data.Count());
        Assert.All(result.Secrets.Data, item => Assert.Contains(item.Id, secretIds));
    }

    [Fact]
    public async Task GetSecretsSyncAsync_ServiceAccountNotRevised_ReturnsNoChanges()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        var apiKeyDetails = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
        var serviceAccountId = apiKeyDetails.ApiKey.ServiceAccountId!.Value;
        await _loginHelper.LoginWithApiKeyAsync(apiKeyDetails);
        await SetupSecretsSyncRequestAsync(org.Id, serviceAccountId);
        await UpdateServiceAccountRevisionAsync(serviceAccountId, DateTime.UtcNow.AddDays(-1));

        var response = await _client.GetAsync($"/organizations/{org.Id}/secrets/sync?lastSyncedDate={DateTime.UtcNow}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SecretsSyncResponseModel>();

        Assert.NotNull(result);
        Assert.False(result.HasChanges);
        Assert.Null(result.Secrets);
    }

    private async Task<(Project Project, List<Guid> secretIds)> CreateSecretsAsync(Guid orgId, int numberToCreate = 3)
    {
        var project = await _projectRepository.CreateAsync(new Project
        {
            Id = new Guid(),
            OrganizationId = orgId,
            Name = _mockEncryptedString
        });

        var secretIds = new List<Guid>();
        for (var i = 0; i < numberToCreate; i++)
        {
            var secret = await _secretRepository.CreateAsync(new Secret
            {
                OrganizationId = orgId,
                Key = _mockEncryptedString,
                Value = _mockEncryptedString,
                Note = _mockEncryptedString,
                Projects = new List<Project>() { project }
            });
            secretIds.Add(secret.Id);
        }

        return (project, secretIds);
    }

    private async Task SetupProjectPermissionAndLoginAsync(PermissionType permissionType, Project project)
    {
        switch (permissionType)
        {
            case PermissionType.RunAsAdmin:
                {
                    await _loginHelper.LoginAsync(_email);
                    break;
                }
            case PermissionType.RunAsUserWithPermission:
                {
                    var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
                    await _loginHelper.LoginAsync(email);

                    var accessPolicies = new List<BaseAccessPolicy>
                {
                    new UserProjectAccessPolicy
                    {
                        GrantedProjectId = project.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true,
                    },
                };
                    await _accessPolicyRepository.CreateManyAsync(accessPolicies);
                    break;
                }
            case PermissionType.RunAsServiceAccountWithPermission:
                {
                    var apiKeyDetails = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
                    await _loginHelper.LoginWithApiKeyAsync(apiKeyDetails);

                    var accessPolicies = new List<BaseAccessPolicy>
                {
                    new ServiceAccountProjectAccessPolicy
                    {
                        GrantedProjectId = project.Id, ServiceAccountId = apiKeyDetails.ApiKey.ServiceAccountId, Read = true, Write = true,
                    },
                };
                    await _accessPolicyRepository.CreateManyAsync(accessPolicies);
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null);
        }
    }

    private async Task<List<Guid>> SetupSecretsSyncRequestAsync(Guid organizationId, Guid serviceAccountId)
    {
        var (project, secretIds) = await CreateSecretsAsync(organizationId);
        var accessPolicies = new List<BaseAccessPolicy>
        {
            new ServiceAccountProjectAccessPolicy
            {
                GrantedProjectId = project.Id, ServiceAccountId = serviceAccountId, Read = true, Write = true
            }
        };
        await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        return secretIds;
    }

    private async Task UpdateServiceAccountRevisionAsync(Guid serviceAccountId, DateTime revisionDate)
    {
        var sa = await _serviceAccountRepository.GetByIdAsync(serviceAccountId);
        sa.RevisionDate = revisionDate;
        await _serviceAccountRepository.ReplaceAsync(sa);
    }

    private async Task<(OrganizationUser, SecretCreateRequestModel)> SetupSecretCreateRequestAsync(
        bool withAccessPolicies)
    {
        var (_, organizationUser) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var request = new SecretCreateRequestModel
        {
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        };

        if (withAccessPolicies)
        {
            request.AccessPoliciesRequests = new SecretAccessPoliciesRequestsModel
            {
                UserAccessPolicyRequests =
                [
                    new AccessPolicyRequest { GranteeId = organizationUser.Id, Read = true, Write = true }
                ],
                GroupAccessPolicyRequests = [],
                ServiceAccountAccessPolicyRequests = []
            };
        }

        return (organizationUser, request);
    }

    private async Task<(OrganizationUser, SecretCreateRequestModel)> SetupSecretWithProjectCreateRequestAsync(
        PermissionType permissionType, bool withAccessPolicies)
    {
        var (org, orgAdminUser) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var project = await _projectRepository.CreateAsync(new Project
        {
            Id = new Guid(),
            OrganizationId = org.Id,
            Name = _mockEncryptedString
        });

        var currentOrganizationUser = orgAdminUser;

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = project.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true
                }
            };
            currentOrganizationUser = orgUser;
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        if (permissionType == PermissionType.RunAsServiceAccountWithPermission)
        {
            var apiKeyDetails = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
            await _loginHelper.LoginWithApiKeyAsync(apiKeyDetails);

            var accessPolicies = new List<BaseAccessPolicy>
            {
                new ServiceAccountProjectAccessPolicy
                {
                    GrantedProjectId = project.Id,
                    ServiceAccountId = apiKeyDetails.ApiKey.ServiceAccountId,
                    Read = true,
                    Write = true
                }
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var secretRequest = new SecretCreateRequestModel
        {
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString,
            ProjectIds = [project.Id]
        };

        if (withAccessPolicies)
        {
            secretRequest.AccessPoliciesRequests = new SecretAccessPoliciesRequestsModel
            {
                UserAccessPolicyRequests =
                [
                    new AccessPolicyRequest { GranteeId = currentOrganizationUser.Id, Read = true, Write = true }
                ],
                GroupAccessPolicyRequests = [],
                ServiceAccountAccessPolicyRequests = []
            };
        }

        return (currentOrganizationUser, secretRequest);
    }

    private async Task<(Secret, SecretUpdateRequestModel)> SetupSecretUpdateRequestAsync(PermissionType permissionType,
        bool withAccessPolicies)
    {
        var (org, adminOrgUser) = await _organizationHelper.Initialize(true, true, true);
        var project = await _projectRepository.CreateAsync(new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = _mockEncryptedString
        });

        await SetupProjectPermissionAndLoginAsync(permissionType, project);

        var secret = await _secretRepository.CreateAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString,
            Projects = permissionType != PermissionType.RunAsAdmin ? new List<Project> { project } : null
        });

        var request = new SecretUpdateRequestModel
        {
            Key = _mockEncryptedString,
            Value =
                "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98xy4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=",
            Note = _mockEncryptedString,
            ProjectIds = permissionType != PermissionType.RunAsAdmin ? [project.Id] : null
        };

        if (!withAccessPolicies)
        {
            return (secret, request);
        }

        request.AccessPoliciesRequests = new SecretAccessPoliciesRequestsModel
        {
            UserAccessPolicyRequests =
                [new AccessPolicyRequest { GranteeId = adminOrgUser.Id, Read = true, Write = true }],
            GroupAccessPolicyRequests = [],
            ServiceAccountAccessPolicyRequests = []
        };

        return (secret, request);
    }

    private async Task<GetSecretsRequestModel> SetupGetSecretsByIdsRequestAsync(Guid organizationId,
        PermissionType permissionType)
    {
        var (project, secretIds) = await CreateSecretsAsync(organizationId);

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    GrantedProjectId = project.Id, OrganizationUserId = orgUser.Id, Read = true, Write = true
                }
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        if (permissionType == PermissionType.RunAsServiceAccountWithPermission)
        {
            var apiKeyDetails = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
            await _loginHelper.LoginWithApiKeyAsync(apiKeyDetails);

            var accessPolicies = new List<BaseAccessPolicy>
            {
                new ServiceAccountProjectAccessPolicy
                {
                    GrantedProjectId = project.Id,
                    ServiceAccountId = apiKeyDetails.ApiKey.ServiceAccountId,
                    Read = true,
                    Write = true
                }
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        return new GetSecretsRequestModel { Ids = secretIds };
    }

    private async Task<GetSecretsRequestModel> SetupNoAccessRequestAsync(Guid organizationId, bool runAsServiceAccount,
        bool partialAccess)
    {
        var (_, secretIds) = await CreateSecretsAsync(organizationId);

        if (runAsServiceAccount)
        {
            var apiKeyDetails = await _organizationHelper.CreateNewServiceAccountApiKeyAsync();
            await _loginHelper.LoginWithApiKeyAsync(apiKeyDetails);

            if (partialAccess)
            {
                var accessPolicies = new List<BaseAccessPolicy>
                {
                    new ServiceAccountSecretAccessPolicy
                    {
                        GrantedSecretId = secretIds[0],
                        ServiceAccountId = apiKeyDetails.ApiKey.ServiceAccountId,
                        Read = true,
                        Write = true
                    }
                };
                await _accessPolicyRepository.CreateManyAsync(accessPolicies);
            }
        }
        else
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

            if (partialAccess)
            {
                var accessPolicies = new List<BaseAccessPolicy>
                {
                    new UserSecretAccessPolicy
                    {
                        GrantedSecretId = secretIds[0],
                        OrganizationUserId = orgUser.Id,
                        Read = true,
                        Write = true
                    }
                };
                await _accessPolicyRepository.CreateManyAsync(accessPolicies);
            }
        }

        return new GetSecretsRequestModel { Ids = secretIds };
    }
}

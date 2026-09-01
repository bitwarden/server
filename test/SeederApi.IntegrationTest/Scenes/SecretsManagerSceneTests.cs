using System.Net;
using System.Text.Json;
using Bit.Core.Billing.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Options;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

/// <summary>
/// Exercises the Secrets Manager seeder scenes end-to-end through POST /seed: seed an SM-enabled org,
/// then create a project, a secret linked to that project, a service account, and access policies. Verifies
/// real rows persist (via the real commercial EF repositories, not the Noops) and that encrypted fields
/// round-trip under the organization key.
/// </summary>
public class SecretsManagerSceneTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;

    public SecretsManagerSceneTests(InPlaySeederApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(_factory.Username, _factory.Password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _client.DeleteAsync("/seed");
        _client.Dispose();
    }

    [Fact]
    public async Task SecretsManagerScenes_SeedProjectSecretServiceAccountAndAccessPolicies()
    {
        var playId = Guid.NewGuid().ToString();

        var ownerUserId = await SeedUserAsync(playId);
        var (organizationId, orgUserId, organizationKeyB64) = await SeedSmOrganizationAsync(playId, ownerUserId);

        var projectResult = await PostSceneAsync(playId, nameof(OrganizationProjectScene), new OrganizationProjectScene.Request
        {
            OrganizationId = organizationId,
            OrganizationKeyB64 = organizationKeyB64,
            Name = "Production"
        });
        var projectId = projectResult.GetProperty("projectId").GetGuid();

        var secretResult = await PostSceneAsync(playId, nameof(OrganizationSecretScene), new OrganizationSecretScene.Request
        {
            OrganizationId = organizationId,
            OrganizationKeyB64 = organizationKeyB64,
            Key = "DB_PASSWORD",
            Value = "s3cret",
            Note = "primary database",
            ProjectIds = [projectId]
        });
        var secretId = secretResult.GetProperty("secretId").GetGuid();

        var serviceAccountResult = await PostSceneAsync(playId, nameof(OrganizationServiceAccountScene), new OrganizationServiceAccountScene.Request
        {
            OrganizationId = organizationId,
            OrganizationKeyB64 = organizationKeyB64,
            Name = "CI Runner"
        });
        var serviceAccountId = serviceAccountResult.GetProperty("serviceAccountId").GetGuid();

        var accessPolicyResult = await PostSceneAsync(playId, nameof(OrganizationAccessPolicyScene), new OrganizationAccessPolicyScene.Request
        {
            OrganizationId = organizationId,
            Grants =
            [
                new OrganizationAccessPolicyScene.Grant
                {
                    GranteeType = AccessPolicySeeder.GranteeType.OrganizationUser,
                    GranteeId = orgUserId,
                    GrantableType = AccessPolicySeeder.GrantableType.Project,
                    GrantableId = projectId,
                    Read = true,
                    Write = true
                },
                new OrganizationAccessPolicyScene.Grant
                {
                    GranteeType = AccessPolicySeeder.GranteeType.ServiceAccount,
                    GranteeId = serviceAccountId,
                    GrantableType = AccessPolicySeeder.GrantableType.Project,
                    GrantableId = projectId,
                    Read = true,
                    Write = false
                }
            ]
        });
        Assert.Equal(2, accessPolicyResult.GetProperty("count").GetInt32());
        Assert.Equal(2, accessPolicyResult.GetProperty("accessPolicyIds").EnumerateArray().Count());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var project = await db.Project.SingleAsync(p => p.Id == projectId);
        Assert.Equal(organizationId, project.OrganizationId);
        Assert.Equal("Production", RustSdkService.DecryptString(project.Name!, organizationKeyB64));

        var secret = await db.Secret.Include(s => s.Projects).SingleAsync(s => s.Id == secretId);
        Assert.Equal(organizationId, secret.OrganizationId);
        Assert.Equal("DB_PASSWORD", RustSdkService.DecryptString(secret.Key!, organizationKeyB64));
        Assert.Equal("s3cret", RustSdkService.DecryptString(secret.Value!, organizationKeyB64));
        Assert.Equal("primary database", RustSdkService.DecryptString(secret.Note!, organizationKeyB64));
        Assert.Contains(secret.Projects!, p => p.Id == projectId);

        var serviceAccount = await db.ServiceAccount.SingleAsync(sa => sa.Id == serviceAccountId);
        Assert.Equal(organizationId, serviceAccount.OrganizationId);
        Assert.Equal("CI Runner", RustSdkService.DecryptString(serviceAccount.Name!, organizationKeyB64));

        var userPolicy = await db.UserProjectAccessPolicy.SingleAsync(ap =>
            ap.OrganizationUserId == orgUserId && ap.GrantedProjectId == projectId);
        Assert.True(userPolicy.Read);
        Assert.True(userPolicy.Write);

        var serviceAccountPolicy = await db.ServiceAccountProjectAccessPolicy.SingleAsync(ap =>
            ap.ServiceAccountId == serviceAccountId && ap.GrantedProjectId == projectId);
        Assert.True(serviceAccountPolicy.Read);
        Assert.False(serviceAccountPolicy.Write);
    }

    [Fact]
    public async Task OrganizationAccessTokenScene_MintsDecodableTokenForServiceAccount()
    {
        var playId = Guid.NewGuid().ToString();

        var ownerUserId = await SeedUserAsync(playId);
        var (organizationId, _, organizationKeyB64) = await SeedSmOrganizationAsync(playId, ownerUserId);

        var serviceAccountResult = await PostSceneAsync(playId, nameof(OrganizationServiceAccountScene), new OrganizationServiceAccountScene.Request
        {
            OrganizationId = organizationId,
            OrganizationKeyB64 = organizationKeyB64,
            Name = "CI Runner"
        });
        var serviceAccountId = serviceAccountResult.GetProperty("serviceAccountId").GetGuid();

        var accessTokenResult = await PostSceneAsync(playId, nameof(OrganizationAccessTokenScene), new OrganizationAccessTokenScene.Request
        {
            OrganizationId = organizationId,
            OrganizationKeyB64 = organizationKeyB64,
            ServiceAccountId = serviceAccountId,
            Name = "deploy token",
            Write = true
        });

        var accessToken = accessTokenResult.GetProperty("accessToken").GetString()!;
        var apiKeyId = accessTokenResult.GetProperty("apiKeyId").GetGuid();

        Assert.Matches(@"^0\.[0-9a-f-]{36}\.[A-Za-z0-9]{30}:[A-Za-z0-9+/=]+$", accessToken);
        Assert.Equal(apiKeyId, Guid.Parse(accessToken.Split(':')[0].Split('.')[1]));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var apiKey = await db.ApiKeys.SingleAsync(k => k.Id == apiKeyId);
        Assert.Equal(serviceAccountId, apiKey.ServiceAccountId);
        Assert.Equal("[\"api.secrets\"]", apiKey.Scope);

        var encryptionKey = Convert.FromBase64String(accessToken.Split(':')[1]);
        var derivedKeyB64 = Convert.ToBase64String(AccessTokenSeeder.DeriveAccessTokenKey(encryptionKey));
        var payload = RustSdkService.DecryptString(apiKey.EncryptedPayload, derivedKeyB64);
        using var payloadDocument = JsonDocument.Parse(payload);
        Assert.Equal(organizationKeyB64, payloadDocument.RootElement.GetProperty("encryptionKey").GetString());
    }

    [Fact]
    public async Task OrganizationAccessTokenScene_ServiceAccountNotInOrganization_ReturnsBadRequest()
    {
        var playId = Guid.NewGuid().ToString();

        var ownerUserId = await SeedUserAsync(playId);
        var (organizationId, _, organizationKeyB64) = await SeedSmOrganizationAsync(playId, ownerUserId);

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(OrganizationAccessTokenScene),
            Arguments = JsonSerializer.SerializeToElement(new OrganizationAccessTokenScene.Request
            {
                OrganizationId = organizationId,
                OrganizationKeyB64 = organizationKeyB64,
                ServiceAccountId = Guid.NewGuid(),
                Name = "deploy token"
            })
        }, playId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not in organization", body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        Assert.False(await db.ApiKeys.AnyAsync(k => k.ServiceAccount.OrganizationId == organizationId));
    }

    [Fact]
    public async Task OrganizationProjectScene_OrganizationWithoutSecretsManager_ReturnsBadRequest()
    {
        var playId = Guid.NewGuid().ToString();

        var ownerUserId = await SeedUserAsync(playId);
        var (organizationId, _, organizationKeyB64) = await SeedNonSmOrganizationAsync(playId, ownerUserId);

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(OrganizationProjectScene),
            Arguments = JsonSerializer.SerializeToElement(new OrganizationProjectScene.Request
            {
                OrganizationId = organizationId,
                OrganizationKeyB64 = organizationKeyB64,
                Name = "Production"
            })
        }, playId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("does not have Secrets Manager enabled", body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        Assert.False(await db.Project.AnyAsync(p => p.OrganizationId == organizationId));
    }

    [Fact]
    public async Task OrganizationSecretScene_ProjectNotInOrganization_ReturnsBadRequest()
    {
        var playId = Guid.NewGuid().ToString();

        var ownerUserId = await SeedUserAsync(playId);
        var (organizationId, _, organizationKeyB64) = await SeedSmOrganizationAsync(playId, ownerUserId);

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(OrganizationSecretScene),
            Arguments = JsonSerializer.SerializeToElement(new OrganizationSecretScene.Request
            {
                OrganizationId = organizationId,
                OrganizationKeyB64 = organizationKeyB64,
                Key = "DB_PASSWORD",
                Value = "s3cret",
                ProjectIds = [Guid.NewGuid()]
            })
        }, playId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not in organization", body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        Assert.False(await db.Secret.AnyAsync(s => s.OrganizationId == organizationId));
    }

    [Fact]
    public async Task OrganizationAccessPolicyScene_GrantableNotInOrganization_ReturnsBadRequest()
    {
        var playId = Guid.NewGuid().ToString();

        var ownerUserId = await SeedUserAsync(playId);
        var (organizationId, orgUserId, _) = await SeedSmOrganizationAsync(playId, ownerUserId);

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(OrganizationAccessPolicyScene),
            Arguments = JsonSerializer.SerializeToElement(new OrganizationAccessPolicyScene.Request
            {
                OrganizationId = organizationId,
                Grants =
                [
                    new OrganizationAccessPolicyScene.Grant
                    {
                        GranteeType = AccessPolicySeeder.GranteeType.OrganizationUser,
                        GranteeId = orgUserId,
                        GrantableType = AccessPolicySeeder.GrantableType.Project,
                        GrantableId = Guid.NewGuid()
                    }
                ]
            })
        }, playId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not in organization", body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        Assert.False(await db.UserProjectAccessPolicy.AnyAsync(ap => ap.OrganizationUserId == orgUserId));
    }

    [Fact]
    public async Task OrganizationAccessPolicyScene_GranteeNotInOrganization_ReturnsBadRequest()
    {
        var playId = Guid.NewGuid().ToString();

        var ownerUserId = await SeedUserAsync(playId);
        var (organizationId, _, organizationKeyB64) = await SeedSmOrganizationAsync(playId, ownerUserId);

        var projectResult = await PostSceneAsync(playId, nameof(OrganizationProjectScene), new OrganizationProjectScene.Request
        {
            OrganizationId = organizationId,
            OrganizationKeyB64 = organizationKeyB64,
            Name = "Production"
        });
        var projectId = projectResult.GetProperty("projectId").GetGuid();

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(OrganizationAccessPolicyScene),
            Arguments = JsonSerializer.SerializeToElement(new OrganizationAccessPolicyScene.Request
            {
                OrganizationId = organizationId,
                Grants =
                [
                    new OrganizationAccessPolicyScene.Grant
                    {
                        GranteeType = AccessPolicySeeder.GranteeType.OrganizationUser,
                        GranteeId = Guid.NewGuid(),
                        GrantableType = AccessPolicySeeder.GrantableType.Project,
                        GrantableId = projectId
                    }
                ]
            })
        }, playId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not in organization", body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        Assert.False(await db.UserProjectAccessPolicy.AnyAsync(ap => ap.GrantedProjectId == projectId));
    }

    [Fact]
    public async Task OrganizationAccessPolicyScene_GroupGranteeNotInOrganization_ReturnsBadRequest()
    {
        var playId = Guid.NewGuid().ToString();

        var ownerUserId = await SeedUserAsync(playId);
        var (organizationId, _, organizationKeyB64) = await SeedSmOrganizationAsync(playId, ownerUserId);

        var projectResult = await PostSceneAsync(playId, nameof(OrganizationProjectScene), new OrganizationProjectScene.Request
        {
            OrganizationId = organizationId,
            OrganizationKeyB64 = organizationKeyB64,
            Name = "Production"
        });
        var projectId = projectResult.GetProperty("projectId").GetGuid();

        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = nameof(OrganizationAccessPolicyScene),
            Arguments = JsonSerializer.SerializeToElement(new OrganizationAccessPolicyScene.Request
            {
                OrganizationId = organizationId,
                Grants =
                [
                    new OrganizationAccessPolicyScene.Grant
                    {
                        GranteeType = AccessPolicySeeder.GranteeType.Group,
                        GranteeId = Guid.NewGuid(),
                        GrantableType = AccessPolicySeeder.GrantableType.Project,
                        GrantableId = projectId
                    }
                ]
            })
        }, playId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not in organization", body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        Assert.False(await db.GroupProjectAccessPolicy.AnyAsync(ap => ap.GrantedProjectId == projectId));
    }

    private async Task<Guid> SeedUserAsync(string playId)
    {
        var result = await PostSceneAsync(playId, "SingleUserScene", new SingleUserScene.Request
        {
            Email = $"user-{Guid.NewGuid():N}@example.com",
            Password = "asdfasdfasdf",
            EmailVerified = true
        });

        return result.GetProperty("userId").GetGuid();
    }

    private async Task<(Guid OrganizationId, Guid OrgUserId, string OrganizationKeyB64)> SeedSmOrganizationAsync(
        string playId, Guid ownerUserId)
    {
        var result = await PostSceneAsync(playId, "SingleOrganizationScene", new SingleOrganizationScene.Request
        {
            OwnerUserId = ownerUserId,
            PlanType = PlanType.EnterpriseAnnually,
            Name = "SM Org",
            Domain = $"sm-{Guid.NewGuid():N}.example.com",
            Seats = 10,
            EnableSecretsManager = true,
            SmSeats = 10,
            SmServiceAccounts = 10
        });

        return (result.GetProperty("organizationId").GetGuid(),
            result.GetProperty("organizationUserId").GetGuid(),
            result.GetProperty("organizationKeyB64").GetString()!);
    }

    private async Task<(Guid OrganizationId, Guid OrgUserId, string OrganizationKeyB64)> SeedNonSmOrganizationAsync(
        string playId, Guid ownerUserId)
    {
        var result = await PostSceneAsync(playId, "SingleOrganizationScene", new SingleOrganizationScene.Request
        {
            OwnerUserId = ownerUserId,
            PlanType = PlanType.EnterpriseAnnually,
            Name = "No SM Org",
            Domain = $"nosm-{Guid.NewGuid():N}.example.com",
            Seats = 10,
            EnableSecretsManager = false,
            Overrides = new OrganizationOverrides { UseSecretsManager = false }
        });

        return (result.GetProperty("organizationId").GetGuid(),
            result.GetProperty("organizationUserId").GetGuid(),
            result.GetProperty("organizationKeyB64").GetString()!);
    }

    private async Task<JsonElement> PostSceneAsync<TRequest>(string playId, string template, TRequest arguments)
    {
        var response = await _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = template,
            Arguments = JsonSerializer.SerializeToElement(arguments)
        }, playId);

        response.EnsureSuccessStatusCode();

        var model = await response.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(model);
        Assert.NotNull(model!.Result);
        return (JsonElement)model.Result!;
    }
}

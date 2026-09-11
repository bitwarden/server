using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.SecretsManager.Enums;
using Bit.Api.IntegrationTest.SecretsManager.Helpers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Api.SecretsManager.Models.Response;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.HttpExtensions;
using Xunit;

namespace Bit.Api.IntegrationTest.SecretsManager.Controllers;

public class SecretVersionsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly ISecretRepository _secretRepository;
    private readonly ISecretVersionRepository _secretVersionRepository;
    private readonly IAccessPolicyRepository _accessPolicyRepository;
    private readonly LoginHelper _loginHelper;

    private string _email = null!;
    private SecretsManagerOrganizationHelper _organizationHelper = null!;

    public SecretVersionsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _secretRepository = _factory.GetService<ISecretRepository>();
        _secretVersionRepository = _factory.GetService<ISecretVersionRepository>();
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

    /// <summary>
    /// Seeds a version the way production does — handed to <see cref="ISecretRepository.UpdateAsync"/>
    /// so it is written inside the owning secret's transaction, with the same retention pruning.
    /// The first call for a secret with no history also backfills a snapshot of that secret's
    /// current value, so it writes two rows rather than one.
    /// </summary>
    private async Task<SecretVersion> AddVersionAsync(Secret secret, string value, DateTime versionDate)
    {
        // AddWithPruningAsync assigns the id on this instance, so it comes back populated.
        var version = new SecretVersion
        {
            SecretId = secret.Id,
            Value = value,
            VersionDate = versionDate
        };

        await _secretRepository.UpdateAsync(secret, null, version);

        return version;
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task GetVersionsBySecretId_SmAccessDenied_NotFound(bool useSecrets, bool accessSecrets, bool organizationEnabled)
    {
        var (org, _) = await _organizationHelper.Initialize(useSecrets, accessSecrets, organizationEnabled);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateWithoutVersionHistoryAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        var response = await _client.GetAsync($"/secrets/{secret.Id}/versions");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(PermissionType.RunAsAdmin)]
    [InlineData(PermissionType.RunAsUserWithPermission)]
    public async Task GetVersionsBySecretId_Success(PermissionType permissionType)
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateWithoutVersionHistoryAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        // Create some versions
        await AddVersionAsync(secret, _mockEncryptedString, DateTime.UtcNow.AddDays(-2));
        await AddVersionAsync(secret, _mockEncryptedString, DateTime.UtcNow.AddDays(-1));

        if (permissionType == PermissionType.RunAsUserWithPermission)
        {
            var (email, orgUser) = await _organizationHelper.CreateNewUser(OrganizationUserType.User, true);
            await _loginHelper.LoginAsync(email);

            var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserSecretAccessPolicy
                {
                    GrantedSecretId = secret.Id,
                    OrganizationUserId = orgUser.Id,
                    Read = true,
                    Write = true
                }
            };
            await _accessPolicyRepository.CreateManyAsync(accessPolicies);
        }

        var response = await _client.GetAsync($"/secrets/{secret.Id}/versions");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<SecretVersionResponseModel>>();

        Assert.NotNull(result);
        // Two seeded versions plus the pre-update value backfilled by the first update.
        Assert.Equal(3, result.Data.Count());
    }

    [Fact]
    public async Task GetVersionById_Success()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateWithoutVersionHistoryAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        var version = await AddVersionAsync(secret, _mockEncryptedString, DateTime.UtcNow);

        var response = await _client.GetAsync($"/secret-versions/{version.Id}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SecretVersionResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(version.Id, result.Id);
        Assert.Equal(secret.Id, result.SecretId);
    }

    [Fact]
    public async Task RestoreVersion_Success()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateWithoutVersionHistoryAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = "OriginalValue",
            Note = _mockEncryptedString
        });

        var version = await AddVersionAsync(secret, "OldValue", DateTime.UtcNow.AddDays(-1));

        var request = new RestoreSecretVersionRequestModel
        {
            VersionId = version.Id
        };

        var response = await _client.PutAsJsonAsync($"/secrets/{secret.Id}/versions/restore", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SecretResponseModel>();

        Assert.NotNull(result);
        Assert.Equal("OldValue", result.Value);
    }

    [Fact]
    public async Task BulkDelete_Success()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateWithoutVersionHistoryAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString
        });

        var version1 = await AddVersionAsync(secret, _mockEncryptedString, DateTime.UtcNow.AddDays(-2));
        var version2 = await AddVersionAsync(secret, _mockEncryptedString, DateTime.UtcNow.AddDays(-1));

        var ids = new List<Guid> { version1.Id, version2.Id };

        var response = await _client.PostAsJsonAsync("/secret-versions/delete", ids);
        response.EnsureSuccessStatusCode();

        // The first update backfilled the pre-update value, so that snapshot is all that remains.
        var versions = (await _secretVersionRepository.GetManyBySecretIdAsync(secret.Id)).ToList();
        Assert.Single(versions);
        Assert.DoesNotContain(versions, v => v.Id == version1.Id || v.Id == version2.Id);
    }

    [Fact]
    public async Task GetVersionsBySecretId_ReturnsOrderedByVersionDate()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateWithoutVersionHistoryAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString,
            // Older than every seeded version, so the backfilled snapshot sorts oldest.
            RevisionDate = DateTime.UtcNow.AddDays(-3)
        });

        // Create versions in random order
        await AddVersionAsync(secret, "Version2", DateTime.UtcNow.AddDays(-1));
        await AddVersionAsync(secret, "Version3", DateTime.UtcNow);
        await AddVersionAsync(secret, "Version1", DateTime.UtcNow.AddDays(-2));

        var response = await _client.GetAsync($"/secrets/{secret.Id}/versions");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<SecretVersionResponseModel>>();

        Assert.NotNull(result);
        // Three seeded versions plus the pre-update value backfilled by the first update.
        Assert.Equal(4, result.Data.Count());

        var versions = result.Data.ToList();
        // Should be ordered by VersionDate descending (newest first)
        Assert.Equal("Version3", versions[0].Value);
        Assert.Equal("Version2", versions[1].Value);
        Assert.Equal("Version1", versions[2].Value);
        Assert.Equal(_mockEncryptedString, versions[3].Value);
    }

    [Fact]
    public async Task GetVersion_PrunesToTenMostRecentVersions()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        // Each write keeps only the ten most recent versions, so the two oldest of these twelve
        // should be pruned as the later ones are written.
        var baseDate = DateTime.UtcNow.AddDays(-20);

        var secret = await _secretRepository.CreateWithoutVersionHistoryAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = _mockEncryptedString,
            Note = _mockEncryptedString,
            // Older than every seeded version, so the value backfilled by the first update is the
            // oldest row and is pruned away rather than displacing a seeded one.
            RevisionDate = baseDate.AddDays(-1)
        });
        for (var i = 0; i < 12; i++)
        {
            await AddVersionAsync(secret, $"Version{i:D2}", baseDate.AddDays(i));
        }

        var response = await _client.GetAsync($"/secrets/{secret.Id}/versions");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<SecretVersionResponseModel>>();

        Assert.NotNull(result);
        var versions = result.Data.ToList();
        Assert.Equal(10, versions.Count);

        // Newest first, with the two oldest dropped rather than the two newest.
        Assert.Equal("Version11", versions[0].Value);
        Assert.Equal("Version02", versions[9].Value);
        Assert.DoesNotContain(versions, v => v.Value == "Version00");
        Assert.DoesNotContain(versions, v => v.Value == "Version01");
        Assert.DoesNotContain(versions, v => v.Value == _mockEncryptedString);
    }
    [Fact]
    public async Task UpdateSecret_WithNoVersionHistory_BackfillsPreviousValue()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        // A secret stored before versioning existed has no version rows at all.
        var secret = await _secretRepository.CreateWithoutVersionHistoryAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = "ValueBeforeVersioning",
            Note = _mockEncryptedString,
            RevisionDate = DateTime.UtcNow.AddDays(-5)
        });

        Assert.Empty(await _secretVersionRepository.GetManyBySecretIdAsync(secret.Id));

        var previousRevisionDate = secret.RevisionDate;
        secret.Value = "NewValue";
        secret.RevisionDate = DateTime.UtcNow;

        await _secretRepository.UpdateAsync(secret, null, new SecretVersion
        {
            SecretId = secret.Id,
            Value = "NewValue",
            VersionDate = secret.RevisionDate
        });

        var versions = (await _secretVersionRepository.GetManyBySecretIdAsync(secret.Id))
            .OrderBy(v => v.VersionDate)
            .ToList();

        Assert.Equal(2, versions.Count);

        // The overwritten value is recoverable, dated to the revision that produced it and
        // attributed to nobody, because that write predates versioning.
        var backfilled = versions[0];
        Assert.Equal("ValueBeforeVersioning", backfilled.Value);
        Assert.Equal(previousRevisionDate, backfilled.VersionDate, TimeSpan.FromSeconds(1));
        Assert.Null(backfilled.EditorOrganizationUserId);
        Assert.Null(backfilled.EditorServiceAccountId);

        Assert.Equal("NewValue", versions[1].Value);
    }

    [Fact]
    public async Task UpdateSecret_WithExistingVersionHistory_DoesNotBackfill()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateWithoutVersionHistoryAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = "FirstValue",
            Note = _mockEncryptedString,
            RevisionDate = DateTime.UtcNow.AddDays(-5)
        });

        // The first update establishes history by backfilling "FirstValue".
        await AddVersionAsync(secret, "SecondValue", DateTime.UtcNow.AddDays(-4));
        Assert.Equal(2, (await _secretVersionRepository.GetManyBySecretIdAsync(secret.Id)).Count());

        // History now exists, so a further update adds only its own snapshot.
        await AddVersionAsync(secret, "ThirdValue", DateTime.UtcNow.AddDays(-3));

        var versions = (await _secretVersionRepository.GetManyBySecretIdAsync(secret.Id)).ToList();
        Assert.Equal(3, versions.Count);
        Assert.Single(versions, v => v.Value == "FirstValue");
    }

    [Fact]
    public async Task UpdateSecret_WithoutNewVersion_DoesNotBackfill()
    {
        var (org, _) = await _organizationHelper.Initialize(true, true, true);
        await _loginHelper.LoginAsync(_email);

        var secret = await _secretRepository.CreateWithoutVersionHistoryAsync(new Secret
        {
            OrganizationId = org.Id,
            Key = _mockEncryptedString,
            Value = "UnchangedValue",
            Note = _mockEncryptedString
        });

        // Callers pass no version when the value did not change; nothing is at risk of being lost,
        // so an edit to other fields must not start a version history.
        secret.Note = "DifferentNote";
        await _secretRepository.UpdateAsync(secret);

        Assert.Empty(await _secretVersionRepository.GetManyBySecretIdAsync(secret.Id));
    }
}

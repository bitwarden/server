using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bit.Core.Vault.Enums;
using Bit.Pam.Enums;
using Bit.Seeder.Queries;
using Bit.Seeder.Scenes;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

/// <summary>
/// Exercises the PAM access connector scenes and queries end-to-end through POST /seed: org, cipher, access
/// connector, target system (pending then sealed), assignment, rotation config, and job. Verifies the rows the
/// real EF repositories persist and what the queries read back.
/// </summary>
public class PamAccessConnectorSceneTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;

    public PamAccessConnectorSceneTests(InPlaySeederApiApplicationFactory factory)
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
    public async Task PamScenes_SeedConnectorTargetConfigAndJob_PersistAndAreQueryable()
    {
        var playId = Guid.NewGuid().ToString();
        var organization = await _client.SeedOrganizationAsync(playId);

        var connector = await _client.PostSceneAsync(playId, nameof(OrganizationAccessConnectorScene),
            new OrganizationAccessConnectorScene.Request
            {
                OrganizationId = organization.OrganizationId,
                Name = "Connector",
                EncryptedPayload = "2.payload",
                Key = "2.key"
            });
        var accessConnectorId = connector.GetProperty("accessConnectorId").GetGuid();
        var apiKeyId = connector.GetProperty("apiKeyId").GetGuid();
        var clientSecret = connector.GetProperty("clientSecret").GetString()!;
        Assert.Equal(30, clientSecret.Length);

        var target = await _client.PostSceneAsync(playId, nameof(PamTargetSystemScene),
            new PamTargetSystemScene.Request { OrganizationId = organization.OrganizationId, Name = "Target" });
        var targetSystemId = target.GetProperty("targetSystemId").GetGuid();

        await _factory.QueryAsync(async db =>
        {
            var pending = await db.PamTargetSystems.SingleAsync(t => t.Id == targetSystemId);
            Assert.Equal(PamTargetSystemStatus.PendingSeal, pending.Status);
            Assert.Equal(PamTargetSystemMethod.Automatic, pending.Method);
            Assert.Null(pending.SealedData);
            return 0;
        });

        await _client.PostSceneAsync(playId, nameof(PamTargetSystemSealScene), new PamTargetSystemSealScene.Request
        {
            TargetSystemId = targetSystemId,
            SealedData = "sealed",
            WrappedKey = "wrapped"
        });

        var assignment = await _client.PostSceneAsync(playId, nameof(PamAccessConnectorTargetAssignmentScene),
            new PamAccessConnectorTargetAssignmentScene.Request
            {
                OrganizationId = organization.OrganizationId,
                AccessConnectorId = accessConnectorId,
                TargetSystemId = targetSystemId
            });
        var assignmentId = assignment.GetProperty("assignmentId").GetGuid();

        var cipher = await _client.PostSceneAsync(playId, nameof(OrganizationLoginCipherScene),
            new OrganizationLoginCipherScene.Request
            {
                UserId = organization.Owner.UserId,
                OrganizationId = organization.OrganizationId,
                OrganizationKeyB64 = organization.OrganizationKeyB64,
                Name = "Rotated login",
                Password = "old-secret"
            });
        var cipherId = cipher.GetProperty("cipherId").GetGuid();

        var config = await _client.PostSceneAsync(playId, nameof(PamRotationConfigScene),
            new PamRotationConfigScene.Request
            {
                OrganizationId = organization.OrganizationId,
                CipherId = cipherId,
                TargetSystemId = targetSystemId,
                SealedData = "config-sealed",
                WrappedKey = "config-wrapped"
            });
        var rotationConfigId = config.GetProperty("rotationConfigId").GetGuid();

        var job = await _client.PostSceneAsync(playId, nameof(PamRotationJobScene), new PamRotationJobScene.Request
        {
            RotationConfigId = rotationConfigId
        });
        var jobId = job.GetProperty("jobId").GetGuid();

        await _factory.QueryAsync(async db =>
        {
            var apiKey = await db.ApiKeys.SingleAsync(k => k.Id == apiKeyId);
            Assert.Null(apiKey.ServiceAccountId);
            Assert.Equal("[\"api.pam.rotation\"]", apiKey.Scope);
            Assert.Equal("2.payload", apiKey.EncryptedPayload);
            Assert.Equal("2.key", apiKey.Key);
            Assert.Equal(
                Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret))),
                apiKey.ClientSecretHash);

            var storedConnector = await db.PamAccessConnectors.SingleAsync(c => c.Id == accessConnectorId);
            Assert.Equal(apiKeyId, storedConnector.ApiKeyId);
            Assert.Equal(organization.OrganizationId, storedConnector.OrganizationId);
            Assert.Equal(PamAccessConnectorStatus.Enabled, storedConnector.Status);

            var sealedTarget = await db.PamTargetSystems.SingleAsync(t => t.Id == targetSystemId);
            Assert.Equal(PamTargetSystemStatus.Active, sealedTarget.Status);
            Assert.Equal("sealed", sealedTarget.SealedData);
            Assert.Equal("wrapped", sealedTarget.WrappedKey);

            Assert.True(await db.PamAccessConnectorTargetAssignments.AnyAsync(a =>
                a.Id == assignmentId && a.AccessConnectorId == accessConnectorId && a.TargetSystemId == targetSystemId));

            var storedConfig = await db.PamRotationConfigs.SingleAsync(c => c.Id == rotationConfigId);
            Assert.True(storedConfig.Enabled);
            Assert.Null(storedConfig.ScheduleCron);
            Assert.True(storedConfig.NextRotationDate > DateTime.UtcNow.AddYears(1));

            var storedJob = await db.PamRotationJobs.SingleAsync(j => j.Id == jobId);
            Assert.Equal(PamRotationJobAction.None, storedJob.Action);
            Assert.Equal(PamRotationSource.OnDemand, storedJob.Source);
            Assert.True(storedJob.ExpirationDate > DateTime.UtcNow);
            return 0;
        });

        var jobState = await _factory.ExecuteQueryAsync<PamRotationJobStateQuery, PamRotationJobStateQuery.Response>(
            q => q.Execute(new PamRotationJobStateQuery.Request { JobId = jobId }));
        Assert.Equal(jobId, jobState.JobId);
        Assert.Equal((int)PamRotationJobAction.None, jobState.Action);
        Assert.Null(jobState.ClaimedByAccessConnectorId);
        Assert.Empty(jobState.Attempts);

        var connectorState = await _factory.ExecuteQueryAsync<PamAccessConnectorStateQuery, PamAccessConnectorStateQuery.Response>(
            q => q.Execute(new PamAccessConnectorStateQuery.Request { AccessConnectorId = accessConnectorId }));
        Assert.Equal((int)PamAccessConnectorStatus.Enabled, connectorState.Status);
        Assert.Null(connectorState.LastHeartbeatDate);

        var cipherState = await _factory.ExecuteQueryAsync<OrganizationCipherQuery, OrganizationCipherQuery.Response>(
            q => q.Execute(new OrganizationCipherQuery.Request { CipherId = cipherId }));
        Assert.Equal((int)CipherType.Login, cipherState.Type);
        Assert.Contains("\"Password\"", cipherState.Data);
        Assert.Null(cipherState.Key);
    }

    [Fact]
    public async Task PamRotationJobScene_ConfigDisabled_FailsAtSeedTime_AndSucceedsWhenDisabledAfterwards()
    {
        var playId = Guid.NewGuid().ToString();
        var organization = await _client.SeedOrganizationAsync(playId);
        var rotationConfigId = await SeedConfigAsync(playId, organization, enabled: false);

        var response = await _client.PostSceneRequestAsync(playId, nameof(PamRotationJobScene),
            new PamRotationJobScene.Request { RotationConfigId = rotationConfigId });
        Assert.False(response.IsSuccessStatusCode);

        await _client.PostSceneAsync(playId, nameof(PamRotationConfigUpdateScene),
            new PamRotationConfigUpdateScene.Request { RotationConfigId = rotationConfigId, Enabled = true });
        var expired = await _client.PostSceneAsync(playId, nameof(PamRotationJobScene),
            new PamRotationJobScene.Request { RotationConfigId = rotationConfigId, ExpiresInSeconds = -60 });
        var jobId = expired.GetProperty("jobId").GetGuid();

        // A second job while the first is unrecorded is refused.
        var duplicate = await _client.PostSceneRequestAsync(playId, nameof(PamRotationJobScene),
            new PamRotationJobScene.Request { RotationConfigId = rotationConfigId });
        Assert.False(duplicate.IsSuccessStatusCode);

        await _client.PostSceneAsync(playId, nameof(PamRotationConfigUpdateScene),
            new PamRotationConfigUpdateScene.Request { RotationConfigId = rotationConfigId, Enabled = false });

        await _factory.QueryAsync(async db =>
        {
            Assert.False((await db.PamRotationConfigs.SingleAsync(c => c.Id == rotationConfigId)).Enabled);
            Assert.True((await db.PamRotationJobs.SingleAsync(j => j.Id == jobId)).ExpirationDate < DateTime.UtcNow);
            return 0;
        });
    }

    [Fact]
    public async Task PamAccessConnectorTargetAssignmentScene_ForeignOrganization_Fails()
    {
        var playId = Guid.NewGuid().ToString();
        var organization = await _client.SeedOrganizationAsync(playId);
        var other = await _client.SeedOrganizationAsync(playId);
        var targetSystemId = (await _client.PostSceneAsync(playId, nameof(PamTargetSystemScene),
            new PamTargetSystemScene.Request { OrganizationId = organization.OrganizationId, Name = "Target" }))
            .GetProperty("targetSystemId").GetGuid();
        var accessConnectorId = (await _client.PostSceneAsync(playId, nameof(OrganizationAccessConnectorScene),
            new OrganizationAccessConnectorScene.Request
            {
                OrganizationId = other.OrganizationId,
                Name = "Connector",
                EncryptedPayload = "2.payload",
                Key = "2.key"
            })).GetProperty("accessConnectorId").GetGuid();

        var response = await _client.PostSceneRequestAsync(playId, nameof(PamAccessConnectorTargetAssignmentScene),
            new PamAccessConnectorTargetAssignmentScene.Request
            {
                OrganizationId = organization.OrganizationId,
                AccessConnectorId = accessConnectorId,
                TargetSystemId = targetSystemId
            });

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationLoginCipherScene_UseCipherKey_SetsItemKey()
    {
        var playId = Guid.NewGuid().ToString();
        var organization = await _client.SeedOrganizationAsync(playId);

        var result = await _client.PostSceneAsync(playId, nameof(OrganizationLoginCipherScene),
            new OrganizationLoginCipherScene.Request
            {
                UserId = organization.Owner.UserId,
                OrganizationId = organization.OrganizationId,
                OrganizationKeyB64 = organization.OrganizationKeyB64,
                Name = "Keyed login",
                Password = "secret",
                UseCipherKey = true
            });
        var cipherId = result.GetProperty("cipherId").GetGuid();

        var cipherState = await _factory.ExecuteQueryAsync<OrganizationCipherQuery, OrganizationCipherQuery.Response>(
            q => q.Execute(new OrganizationCipherQuery.Request { CipherId = cipherId }));
        Assert.False(string.IsNullOrEmpty(cipherState.Key));
    }

    [Fact]
    public async Task DeleteSeed_RemovesAccessConnectorApiKey()
    {
        var playId = Guid.NewGuid().ToString();
        var organization = await _client.SeedOrganizationAsync(playId);
        var apiKeyId = (await _client.PostSceneAsync(playId, nameof(OrganizationAccessConnectorScene),
            new OrganizationAccessConnectorScene.Request
            {
                OrganizationId = organization.OrganizationId,
                Name = "Connector",
                EncryptedPayload = "2.payload",
                Key = "2.key"
            })).GetProperty("apiKeyId").GetGuid();

        var response = await _client.DeleteAsync($"/seed/{playId}");
        response.EnsureSuccessStatusCode();

        var remaining = await _factory.QueryAsync(db => db.ApiKeys.CountAsync(k => k.Id == apiKeyId));
        Assert.Equal(0, remaining);
    }

    private async Task<Guid> SeedConfigAsync(string playId, SeededOrganization organization, bool enabled)
    {
        var targetSystemId = (await _client.PostSceneAsync(playId, nameof(PamTargetSystemScene),
            new PamTargetSystemScene.Request { OrganizationId = organization.OrganizationId, Name = "Target" }))
            .GetProperty("targetSystemId").GetGuid();
        await _client.PostSceneAsync(playId, nameof(PamTargetSystemSealScene), new PamTargetSystemSealScene.Request
        {
            TargetSystemId = targetSystemId,
            SealedData = "sealed",
            WrappedKey = "wrapped"
        });
        var cipherId = (await _client.PostSceneAsync(playId, nameof(OrganizationLoginCipherScene),
            new OrganizationLoginCipherScene.Request
            {
                UserId = organization.Owner.UserId,
                OrganizationId = organization.OrganizationId,
                OrganizationKeyB64 = organization.OrganizationKeyB64,
                Name = "Rotated login"
            })).GetProperty("cipherId").GetGuid();

        return (await _client.PostSceneAsync(playId, nameof(PamRotationConfigScene), new PamRotationConfigScene.Request
        {
            OrganizationId = organization.OrganizationId,
            CipherId = cipherId,
            TargetSystemId = targetSystemId,
            SealedData = "config-sealed",
            WrappedKey = "config-wrapped",
            Enabled = enabled
        })).GetProperty("rotationConfigId").GetGuid();
    }
}

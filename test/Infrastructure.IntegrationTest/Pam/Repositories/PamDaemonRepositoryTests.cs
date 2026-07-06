using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class PamDaemonRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_ThenRead_RoundTripsFields(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository,
        IPamDaemonRepository pamDaemonRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var apiKey = await apiKeyRepository.CreateAsync(BuildDaemonApiKey());

        var daemon = await pamDaemonRepository.CreateAsync(new PamDaemon
        {
            OrganizationId = organization.Id,
            Name = "prod-daemon",
            ApiKeyId = apiKey.Id,
            Status = PamDaemonStatus.Enrolled,
        });

        var persisted = await pamDaemonRepository.GetByIdAsync(daemon.Id);

        Assert.NotNull(persisted);
        Assert.Equal(organization.Id, persisted!.OrganizationId);
        Assert.Equal("prod-daemon", persisted.Name);
        Assert.Equal(apiKey.Id, persisted.ApiKeyId);
        Assert.Equal(PamDaemonStatus.Enrolled, persisted.Status);
        Assert.Null(persisted.LastHeartbeatAt);
    }

    // PamDaemonClientProvider's token-issuance lookup: the daemon plus its organization's licensing flags, keyed by
    // the ApiKey credential rather than the daemon's own id. Enabled and UsePam are independent columns on
    // Organization -- flip one without the other to prove the mapping does not swap or conflate them.
    [DatabaseTheory, DatabaseData]
    public async Task GetDetailsByApiKeyIdAsync_ReturnsDaemonWithOrganizationLicensingFlags(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository,
        IPamDaemonRepository pamDaemonRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        Assert.True(organization.Enabled);
        Assert.True(organization.UsePam);
        var apiKey = await apiKeyRepository.CreateAsync(BuildDaemonApiKey());
        var daemon = await pamDaemonRepository.CreateAsync(new PamDaemon
        {
            OrganizationId = organization.Id,
            Name = "prod-daemon",
            ApiKeyId = apiKey.Id,
            Status = PamDaemonStatus.Enrolled,
        });

        var details = await pamDaemonRepository.GetDetailsByApiKeyIdAsync(apiKey.Id);

        Assert.NotNull(details);
        Assert.Equal(daemon.Id, details!.Id);
        Assert.Equal(PamDaemonStatus.Enrolled, details.Status);
        Assert.True(details.OrganizationEnabled);
        Assert.True(details.OrganizationUsePam);

        // Flip UsePam only: OrganizationEnabled must stay true while OrganizationUsePam flips, proving the two
        // columns map independently rather than one driving both.
        organization.UsePam = false;
        await organizationRepository.ReplaceAsync(organization);

        var afterLicenseLapse = await pamDaemonRepository.GetDetailsByApiKeyIdAsync(apiKey.Id);
        Assert.NotNull(afterLicenseLapse);
        Assert.True(afterLicenseLapse!.OrganizationEnabled);
        Assert.False(afterLicenseLapse.OrganizationUsePam);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetDetailsByApiKeyIdAsync_UnknownApiKeyId_ReturnsNull(
        IPamDaemonRepository pamDaemonRepository)
    {
        Assert.Null(await pamDaemonRepository.GetDetailsByApiKeyIdAsync(Guid.NewGuid()));
    }

    // The daemon-facing request filter calls this on every request; the WHERE guard on the sproc turns a poll that
    // arrives before MinInterval has elapsed into a no-op, and only a poll arriving after it actually bumps the
    // column.
    [DatabaseTheory, DatabaseData]
    public async Task UpdateHeartbeatAsync_ConditionalBump(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository,
        IPamDaemonRepository pamDaemonRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var apiKey = await apiKeyRepository.CreateAsync(BuildDaemonApiKey());
        var daemon = await pamDaemonRepository.CreateAsync(new PamDaemon
        {
            OrganizationId = organization.Id,
            Name = "prod-daemon",
            ApiKeyId = apiKey.Id,
            Status = PamDaemonStatus.Enrolled,
        });
        var minInterval = TimeSpan.FromSeconds(15);
        var firstHeartbeat = DateTime.UtcNow;

        // First heartbeat: LastHeartbeatAt was null, so it always bumps.
        await pamDaemonRepository.UpdateHeartbeatAsync(daemon.Id, firstHeartbeat, minInterval);
        var afterFirst = await pamDaemonRepository.GetByIdAsync(daemon.Id);
        Assert.NotNull(afterFirst!.LastHeartbeatAt);
        var recordedFirst = afterFirst.LastHeartbeatAt!.Value;

        // Second poll arrives well within MinInterval: the guard's WHERE clause keeps this a no-op.
        await pamDaemonRepository.UpdateHeartbeatAsync(daemon.Id, firstHeartbeat.AddSeconds(5), minInterval);
        var afterSecond = await pamDaemonRepository.GetByIdAsync(daemon.Id);
        Assert.Equal(recordedFirst, afterSecond!.LastHeartbeatAt);

        // Third poll arrives after MinInterval has elapsed since the last recorded bump: it updates.
        var thirdHeartbeat = firstHeartbeat.AddSeconds(20);
        await pamDaemonRepository.UpdateHeartbeatAsync(daemon.Id, thirdHeartbeat, minInterval);
        var afterThird = await pamDaemonRepository.GetByIdAsync(daemon.Id);
        Assert.Equal(thirdHeartbeat, afterThird!.LastHeartbeatAt);
        Assert.NotEqual(recordedFirst, afterThird.LastHeartbeatAt);
    }

    [DatabaseTheory, DatabaseData]
    public async Task Assignment_CreateExistsDeleteReadByOrganization_RoundTrips(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IPamDaemonRepository pamDaemonRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await pamTargetSystemRepository.CreateAsync(new PamTargetSystem
        {
            OrganizationId = organization.Id,
            Name = "target",
            Method = PamTargetSystemMethod.Automatic,
            Kind = PamTargetSystemKind.Mssql,
            Status = PamTargetSystemStatus.Active,
            CreationDate = now,
            RevisionDate = now,
        });
        var apiKey = await apiKeyRepository.CreateAsync(BuildDaemonApiKey());
        var daemon = await pamDaemonRepository.CreateAsync(new PamDaemon
        {
            OrganizationId = organization.Id,
            Name = "daemon",
            ApiKeyId = apiKey.Id,
            Status = PamDaemonStatus.Enrolled,
        });

        Assert.False(await pamDaemonRepository.AssignmentExistsAsync(daemon.Id, target.Id));
        Assert.Empty(await pamDaemonRepository.GetAssignmentsByOrganizationIdAsync(organization.Id));

        var assignment = new PamDaemonTargetAssignment
        {
            Id = CoreHelpers.GenerateComb(),
            DaemonId = daemon.Id,
            TargetSystemId = target.Id,
            OrganizationId = organization.Id,
            CreationDate = now,
        };
        await pamDaemonRepository.CreateAssignmentAsync(assignment);

        Assert.True(await pamDaemonRepository.AssignmentExistsAsync(daemon.Id, target.Id));
        var assignments = await pamDaemonRepository.GetAssignmentsByOrganizationIdAsync(organization.Id);
        var persisted = Assert.Single(assignments);
        Assert.Equal(assignment.Id, persisted.Id);
        Assert.Equal(daemon.Id, persisted.DaemonId);
        Assert.Equal(target.Id, persisted.TargetSystemId);

        await pamDaemonRepository.DeleteAssignmentAsync(daemon.Id, target.Id);

        Assert.False(await pamDaemonRepository.AssignmentExistsAsync(daemon.Id, target.Id));
        Assert.Empty(await pamDaemonRepository.GetAssignmentsByOrganizationIdAsync(organization.Id));
    }

    // PamDaemon_Update is narrow: only Name/Status/RevisionDate are declared sproc parameters. Mutate ApiKeyId and
    // OrganizationId on the in-memory entity too before calling ReplaceAsync -- if the override were widened to a
    // whole-entity write those garbage values would persist; instead they must be silently ignored.
    [DatabaseTheory, DatabaseData]
    public async Task ReplaceAsync_OnlyPersistsNameStatusRevisionDate(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository,
        IPamDaemonRepository pamDaemonRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var apiKey = await apiKeyRepository.CreateAsync(BuildDaemonApiKey());
        var daemon = await pamDaemonRepository.CreateAsync(new PamDaemon
        {
            OrganizationId = organization.Id,
            Name = "daemon",
            ApiKeyId = apiKey.Id,
            Status = PamDaemonStatus.Enrolled,
        });
        var originalOrganizationId = daemon.OrganizationId;
        var originalApiKeyId = daemon.ApiKeyId;
        var newRevisionDate = DateTime.UtcNow.AddMinutes(10);

        daemon.Name = "renamed-daemon";
        daemon.Status = PamDaemonStatus.Revoked;
        daemon.RevisionDate = newRevisionDate;
        daemon.OrganizationId = Guid.NewGuid();
        daemon.ApiKeyId = Guid.NewGuid();
        await pamDaemonRepository.ReplaceAsync(daemon);

        var persisted = await pamDaemonRepository.GetByIdAsync(daemon.Id);
        Assert.NotNull(persisted);
        Assert.Equal("renamed-daemon", persisted!.Name);
        Assert.Equal(PamDaemonStatus.Revoked, persisted.Status);
        Assert.Equal(newRevisionDate, persisted.RevisionDate);
        Assert.Equal(originalOrganizationId, persisted.OrganizationId);
        Assert.Equal(originalApiKeyId, persisted.ApiKeyId);
    }

    private static ApiKey BuildDaemonApiKey(string identifier = "daemon") => new()
    {
        ServiceAccountId = null,
        Name = $"{identifier}-{Guid.NewGuid()}",
        Scope = """["api.pam.rotation"]""",
        EncryptedPayload = "encrypted-payload",
        Key = "encrypted-key",
    };
}

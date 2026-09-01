using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class PamRotationConfigRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_ThenRead_RoundTripsFields(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateAutomaticTargetAsync(pamTargetSystemRepository, organization.Id, now);
        var cipher = await CreateCipherAsync(cipherRepository, organization.Id);

        var config = await pamRotationConfigRepository.CreateAsync(new PamRotationConfig
        {
            OrganizationId = organization.Id,
            CipherId = cipher.Id,
            TargetSystemId = target.Id,
            AccountIdentity = "svc-rotation-account",
            TerminateSessions = true,
            ScheduleCron = "0 0/15 * * * ?",
            RotateOnAccessEnd = true,
            NextRotationAt = now.AddMinutes(15),
            Enabled = true,
            CreationDate = now,
            RevisionDate = now,
        });

        var persisted = await pamRotationConfigRepository.GetByIdAsync(config.Id);
        Assert.NotNull(persisted);
        Assert.Equal(organization.Id, persisted!.OrganizationId);
        Assert.Equal(cipher.Id, persisted.CipherId);
        Assert.Equal(target.Id, persisted.TargetSystemId);
        Assert.Equal("svc-rotation-account", persisted.AccountIdentity);
        Assert.True(persisted.TerminateSessions);
        Assert.Equal("0 0/15 * * * ?", persisted.ScheduleCron);
        Assert.True(persisted.RotateOnAccessEnd);
        Assert.Equal(now.AddMinutes(15), persisted.NextRotationAt.Value, LaxDateTimeComparer.Default);
        Assert.True(persisted.Enabled);
        Assert.Null(persisted.LastRotationAt);

        var byCipher = await pamRotationConfigRepository.GetByCipherIdAsync(cipher.Id);
        Assert.NotNull(byCipher);
        Assert.Equal(config.Id, byCipher!.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetByCipherIdAsync_NoConfig_ReturnsNull(IPamRotationConfigRepository pamRotationConfigRepository)
    {
        Assert.Null(await pamRotationConfigRepository.GetByCipherIdAsync(Guid.NewGuid()));
    }

    // OneConfigPerCipher (IX_PamRotationConfig_CipherId): a second config for a cipher that already has one hits the
    // unique index and throws -- PamRotationConfigRepository does not catch this the way AccessLeaseRepository does
    // for its own unique-index backstop, so the caller (CreateRotationConfigCommand) is expected to have already
    // guarded against it via GetByCipherIdAsync. Both ORMs enforce it; only the exception type differs.
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_SecondConfigForSameCipher_Throws(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateAutomaticTargetAsync(pamTargetSystemRepository, organization.Id, now);
        var cipher = await CreateCipherAsync(cipherRepository, organization.Id);

        await pamRotationConfigRepository.CreateAsync(BuildConfig(organization.Id, cipher.Id, target.Id, now));

        // The exception type is provider-specific (SqlException on MSSQL, DbUpdateException through EF), so assert
        // the invariant rather than the type: the write fails and the cipher still has exactly its first config.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            pamRotationConfigRepository.CreateAsync(BuildConfig(organization.Id, cipher.Id, target.Id, now)));
        Assert.NotNull(await pamRotationConfigRepository.GetByCipherIdAsync(cipher.Id));
    }

    // The sweep's due phase: enabled + automatic + active-target configs whose schedule has come due, with no active
    // job already in flight. Paused, disabled-target, not-yet-due, and manual configs are all excluded.
    [DatabaseTheory, DatabaseData]
    public async Task GetManyDueAsync_ReturnsOnlyEnabledAutomaticActiveDueConfigs(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var activeTarget = await CreateAutomaticTargetAsync(pamTargetSystemRepository, organization.Id, now);
        var disabledTarget = await CreateAutomaticTargetAsync(pamTargetSystemRepository, organization.Id, now,
            status: PamTargetSystemStatus.Disabled);
        var manualTarget = await pamTargetSystemRepository.CreateAsync(new PamTargetSystem
        {
            OrganizationId = organization.Id,
            Name = "manual",
            Method = PamTargetSystemMethod.Manual,
            Status = PamTargetSystemStatus.Active,
            CreationDate = now,
            RevisionDate = now,
        });

        var due = await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, (await CreateCipherAsync(cipherRepository, organization.Id)).Id, activeTarget.Id, now,
                nextRotationAt: now.AddMinutes(-1)));
        await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, (await CreateCipherAsync(cipherRepository, organization.Id)).Id, activeTarget.Id, now,
                enabled: false, nextRotationAt: now.AddMinutes(-1)));
        await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, (await CreateCipherAsync(cipherRepository, organization.Id)).Id, disabledTarget.Id, now,
                nextRotationAt: now.AddMinutes(-1)));
        await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, (await CreateCipherAsync(cipherRepository, organization.Id)).Id, manualTarget.Id, now,
                nextRotationAt: now.AddMinutes(-1)));
        await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, (await CreateCipherAsync(cipherRepository, organization.Id)).Id, activeTarget.Id, now,
                nextRotationAt: now.AddHours(1)));
        await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, (await CreateCipherAsync(cipherRepository, organization.Id)).Id, activeTarget.Id, now,
                nextRotationAt: null));

        // The sweep is deliberately global -- it runs across every organization -- so scope the assertion to this
        // test's own rows rather than to the whole result, which any other seeded organization would perturb.
        var dueConfigs = await pamRotationConfigRepository.GetManyDueAsync(now);

        var row = Assert.Single(dueConfigs, c => c.OrganizationId == organization.Id);
        Assert.Equal(due.Id, row.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task AnyByTargetSystemAsync_ReflectsConfigsOnTarget(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateAutomaticTargetAsync(pamTargetSystemRepository, organization.Id, now);
        var otherTarget = await CreateAutomaticTargetAsync(pamTargetSystemRepository, organization.Id, now);

        Assert.False(await pamRotationConfigRepository.AnyByTargetSystemAsync(target.Id));

        var cipher = await CreateCipherAsync(cipherRepository, organization.Id);
        await pamRotationConfigRepository.CreateAsync(BuildConfig(organization.Id, cipher.Id, target.Id, now));

        // Unlike the WithTerminateSessions sibling, the target-delete guard does not care what a config opts into:
        // any config naming the target keeps it.
        Assert.True(await pamRotationConfigRepository.AnyByTargetSystemAsync(target.Id));
        Assert.False(await pamRotationConfigRepository.AnyByTargetSystemAsync(otherTarget.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task AnyByTargetSystemWithTerminateSessionsAsync_ReflectsConfigsOnTarget(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateAutomaticTargetAsync(pamTargetSystemRepository, organization.Id, now,
            supportsSessionTermination: true);
        var otherTarget = await CreateAutomaticTargetAsync(pamTargetSystemRepository, organization.Id, now,
            supportsSessionTermination: true);

        Assert.False(await pamRotationConfigRepository.AnyByTargetSystemWithTerminateSessionsAsync(target.Id));

        var cipher = await CreateCipherAsync(cipherRepository, organization.Id);
        await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, cipher.Id, target.Id, now, terminateSessions: true));
        var otherCipher = await CreateCipherAsync(cipherRepository, organization.Id);
        await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, otherCipher.Id, otherTarget.Id, now, terminateSessions: false));

        Assert.True(await pamRotationConfigRepository.AnyByTargetSystemWithTerminateSessionsAsync(target.Id));
        Assert.False(await pamRotationConfigRepository.AnyByTargetSystemWithTerminateSessionsAsync(otherTarget.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteWithJobsAsync_CascadesJobsAndAttempts(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateAutomaticTargetAsync(pamTargetSystemRepository, organization.Id, now);
        var daemon = await CreateEnrolledDaemonAsync(apiKeyRepository, pamDaemonRepository, organization.Id);
        await AssignAsync(pamDaemonRepository, daemon.Id, target.Id, organization.Id, now);
        var cipher = await CreateCipherAsync(cipherRepository, organization.Id);
        var config = await pamRotationConfigRepository.CreateAsync(BuildConfig(organization.Id, cipher.Id, target.Id, now));
        var job = BuildPendingJob(config.Id, now);
        Assert.Equal(PamRotationJobCreateOutcome.Created, await pamRotationJobRepository.CreateGuardedAsync(job));
        var claim = await pamRotationJobRepository.ClaimAsync(job.Id, daemon.Id, now, TimeSpan.FromMinutes(15));
        Assert.Equal(PamRotationClaimOutcome.Claimed, claim.Outcome);

        // While a daemon holds the claim the delete is refused outright: tearing the job out from under it would
        // leave the target rotated and the vault holding the old secret, with no attempt row to record the drift.
        Assert.False(await pamRotationConfigRepository.DeleteWithJobsAsync(config.Id));
        Assert.NotNull(await pamRotationConfigRepository.GetByIdAsync(config.Id));
        Assert.NotNull(await pamRotationJobRepository.GetByIdAsync(job.Id));

        // Once the job reaches a terminal state the config and its whole job/attempt history cascade away.
        var failure = await pamRotationJobRepository.MarkAttemptErroredAsync(claim.AttemptId!.Value, daemon.Id,
            "boom", PamRotationSyncState.TargetUnchanged, now, maxAttempts: 1, retryBaseDelay: TimeSpan.FromMinutes(1));
        Assert.Equal(PamRotationJobStatus.Failed, failure.JobStatus);

        Assert.True(await pamRotationConfigRepository.DeleteWithJobsAsync(config.Id));

        Assert.Null(await pamRotationConfigRepository.GetByIdAsync(config.Id));
        Assert.Null(await pamRotationJobRepository.GetByIdAsync(job.Id));
        Assert.Null(await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId!.Value));
    }

    // The config detail page's header projection: target display fields denormalized, plus a computed HasActiveJob
    // that flips back to false once the job leaves Pending/Claimed (here, once it succeeds).
    [DatabaseTheory, DatabaseData]
    public async Task GetDetailsByIdAsync_ProjectsTargetFieldsAndHasActiveJob(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateAutomaticTargetAsync(pamTargetSystemRepository, organization.Id, now);
        var daemon = await CreateEnrolledDaemonAsync(apiKeyRepository, pamDaemonRepository, organization.Id);
        await AssignAsync(pamDaemonRepository, daemon.Id, target.Id, organization.Id, now);
        var cipher = await CreateCipherAsync(cipherRepository, organization.Id);
        var config = await pamRotationConfigRepository.CreateAsync(BuildConfig(organization.Id, cipher.Id, target.Id, now));

        var beforeJob = await pamRotationConfigRepository.GetDetailsByIdAsync(config.Id);
        Assert.NotNull(beforeJob);
        Assert.Equal(target.Name, beforeJob!.TargetSystemName);
        Assert.Equal(PamTargetSystemMethod.Automatic, beforeJob.TargetSystemMethod);
        Assert.False(beforeJob.HasActiveJob);

        var job = BuildPendingJob(config.Id, now);
        await pamRotationJobRepository.CreateGuardedAsync(job);
        var withActiveJob = await pamRotationConfigRepository.GetDetailsByIdAsync(config.Id);
        Assert.True(withActiveJob!.HasActiveJob);

        var claim = await pamRotationJobRepository.ClaimAsync(job.Id, daemon.Id, now, TimeSpan.FromMinutes(15));
        await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, daemon.Id, "{\"rotated\":true}", cipher.RevisionDate, now);
        await pamRotationJobRepository.MarkAttemptRotatedAsync(
            claim.AttemptId!.Value, daemon.Id, PamSessionTerminationOutcome.NotRequested, now);

        var afterSuccess = await pamRotationConfigRepository.GetDetailsByIdAsync(config.Id);
        Assert.NotNull(afterSuccess);
        Assert.False(afterSuccess!.HasActiveJob);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetDetailsByIdAsync_UnknownId_ReturnsNull(IPamRotationConfigRepository pamRotationConfigRepository)
    {
        Assert.Null(await pamRotationConfigRepository.GetDetailsByIdAsync(Guid.NewGuid()));
    }

    private static async Task<PamTargetSystem> CreateAutomaticTargetAsync(
        IPamTargetSystemRepository pamTargetSystemRepository, Guid organizationId, DateTime now,
        PamTargetSystemStatus status = PamTargetSystemStatus.Active, bool? supportsSessionTermination = null)
        => await pamTargetSystemRepository.CreateAsync(new PamTargetSystem
        {
            OrganizationId = organizationId,
            Name = $"target-{Guid.NewGuid()}",
            Method = PamTargetSystemMethod.Automatic,
            Kind = PamTargetSystemKind.Mssql,
            SupportsSessionTermination = supportsSessionTermination,
            Status = status,
            CreationDate = now,
            RevisionDate = now,
        });

    private static async Task<PamDaemon> CreateEnrolledDaemonAsync(
        IApiKeyRepository apiKeyRepository, IPamDaemonRepository pamDaemonRepository, Guid organizationId)
    {
        var apiKey = await apiKeyRepository.CreateAsync(new ApiKey
        {
            ServiceAccountId = null,
            Name = $"daemon-{Guid.NewGuid()}",
            Scope = """["api.pam.rotation"]""",
            EncryptedPayload = "encrypted-payload",
            Key = "encrypted-key",
        });
        return await pamDaemonRepository.CreateAsync(new PamDaemon
        {
            OrganizationId = organizationId,
            Name = $"daemon-{Guid.NewGuid()}",
            ApiKeyId = apiKey.Id,
            Status = PamAccessConnectorStatus.Enabled,
        });
    }

    private static async Task AssignAsync(
        IPamDaemonRepository pamDaemonRepository, Guid daemonId, Guid targetSystemId, Guid organizationId, DateTime now)
        => await pamDaemonRepository.CreateAssignmentAsync(new PamDaemonTargetAssignment
        {
            Id = CombGuid.Generate(),
            DaemonId = daemonId,
            TargetSystemId = targetSystemId,
            OrganizationId = organizationId,
            CreationDate = now,
        });

    private static async Task<Cipher> CreateCipherAsync(ICipherRepository cipherRepository, Guid organizationId)
        => await cipherRepository.CreateAsync(new Cipher
        {
            OrganizationId = organizationId,
            Type = CipherType.Login,
            Data = "{\"originalSecret\":true}",
        });

    private static PamRotationConfig BuildConfig(Guid organizationId, Guid cipherId, Guid targetSystemId, DateTime now,
        bool enabled = true, bool terminateSessions = false, DateTime? nextRotationAt = null) => new()
        {
            OrganizationId = organizationId,
            CipherId = cipherId,
            TargetSystemId = targetSystemId,
            AccountIdentity = "svc-account",
            TerminateSessions = terminateSessions,
            RotateOnAccessEnd = false,
            NextRotationAt = nextRotationAt,
            Enabled = enabled,
            CreationDate = now,
            RevisionDate = now,
        };

    private static PamRotationJob BuildPendingJob(Guid configId, DateTime now) => new()
    {
        Id = CombGuid.Generate(),
        RotationConfigId = configId,
        Source = PamRotationSource.Scheduled,
        Status = PamRotationJobStatus.Pending,
        CreationDate = now,
        NextClaimableAt = now,
        ExpiresAt = now.AddHours(1),
    };
}

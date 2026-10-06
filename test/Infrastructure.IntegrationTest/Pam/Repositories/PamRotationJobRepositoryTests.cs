using Bit.Core.AdminConsole.Entities;
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

public class PamRotationJobRepositoryTests
{
    private static readonly TimeSpan _releaseDelay = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan _offlineAfter = TimeSpan.FromMinutes(2);

    [DatabaseTheory, DatabaseData]
    public async Task CreateGuardedAsync_EnforcesAtMostOneActiveJobPerConfig(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);

        // A second offer for the same config must be refused as ActiveJobExists.
        var second = BuildPendingJob(fixture.Config.Id, fixture.Now);
        Assert.Equal(PamRotationJobCreateOutcome.ActiveJobExists,
            await pamRotationJobRepository.CreateGuardedAsync(second));
        Assert.Null(await pamRotationJobRepository.GetByIdAsync(second.Id));
        Assert.NotNull(await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateGuardedAsync_PausedConfig_ConfigNotOfferable(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateAutomaticTargetAsync(pamTargetSystemRepository, organization.Id, now);
        var cipher = await CreateCipherAsync(cipherRepository, organization.Id);
        var config = await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, cipher.Id, target.Id, now, enabled: false));

        var job = BuildPendingJob(config.Id, now);
        Assert.Equal(PamRotationJobCreateOutcome.ConfigNotOfferable,
            await pamRotationJobRepository.CreateGuardedAsync(job));
        Assert.Null(await pamRotationJobRepository.GetByIdAsync(job.Id));
    }

    // has_active_job ends when the sweep records the timeout, so its reschedule lands before the next offer.
    [DatabaseTheory, DatabaseData]
    public async Task CreateGuardedAsync_TimedOutJobHoldsTheConfigUntilTheSweepRecordsIt(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: now.AddHours(-2), expiresAt: now.AddMinutes(-1));

        Assert.Equal(PamRotationJobCreateOutcome.ActiveJobExists,
            await pamRotationJobRepository.CreateGuardedAsync(BuildPendingJob(fixture.Config.Id, now)));

        Assert.Contains(await pamRotationJobRepository.TimeoutDueAsync(now), r => r.JobId == fixture.Job.Id);

        Assert.Equal(PamRotationJobCreateOutcome.Created,
            await pamRotationJobRepository.CreateGuardedAsync(BuildPendingJob(fixture.Config.Id, now)));
    }

    // The config-side gates use the same rule. Kept in this class, whose sweeps run one at a time, so no other
    // test's sweep can record the timeout mid-way.
    [DatabaseTheory, DatabaseData]
    public async Task TimedOutJob_HoldsTheDueScanAndDeleteUntilTheSweepRecordsIt(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: now.AddHours(-2), expiresAt: now.AddMinutes(-1));
        fixture.Config.NextRotationAt = now.AddMinutes(-1);
        await pamRotationConfigRepository.ReplaceAsync(fixture.Config);

        Assert.True((await pamRotationConfigRepository.GetDetailsByIdAsync(fixture.Config.Id))!.HasActiveJob);
        Assert.DoesNotContain(await pamRotationConfigRepository.GetManyDueAsync(now), c => c.Id == fixture.Config.Id);
        Assert.False(await pamRotationConfigRepository.DeleteWithJobsAsync(fixture.Config.Id));

        Assert.Contains(await pamRotationJobRepository.TimeoutDueAsync(now), r => r.JobId == fixture.Job.Id);

        Assert.False((await pamRotationConfigRepository.GetDetailsByIdAsync(fixture.Config.Id))!.HasActiveJob);
        Assert.Contains(await pamRotationConfigRepository.GetManyDueAsync(now), c => c.Id == fixture.Config.Id);
        // The journal row goes with its job.
        Assert.True(await pamRotationConfigRepository.DeleteWithJobsAsync(fixture.Config.Id));
        Assert.Null(await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id));
    }

    // First-claim-wins under real contention: two access connectors race the same Pending job.
    [DatabaseTheory, DatabaseData]
    public async Task ClaimAsync_ConcurrentDoubleClaim_ExactlyOneWinner(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var rival = await CreateEnrolledAccessConnectorAsync(
            apiKeyRepository, pamAccessConnectorRepository, fixture.Organization.Id);
        await AssignAsync(
            pamAccessConnectorRepository, rival.Id, fixture.Target.Id, fixture.Organization.Id, fixture.Now);
        var claimNow = fixture.Now;

        var results = await Task.WhenAll(
            pamRotationJobRepository.ClaimAsync(fixture.Job.Id, fixture.AccessConnector.Id, claimNow, _releaseDelay),
            pamRotationJobRepository.ClaimAsync(fixture.Job.Id, rival.Id, claimNow, _releaseDelay));

        var winner = Assert.Single(results, r => r.Outcome == PamRotationClaimOutcome.Claimed);
        var loser = Assert.Single(results, r => r.Outcome != PamRotationClaimOutcome.Claimed);
        Assert.Equal(PamRotationClaimOutcome.NotClaimable, loser.Outcome);
        Assert.Null(loser.AttemptId);

        // The winner's snapshot is fully populated, including the claim lease's deadline.
        Assert.NotNull(winner.AttemptId);
        Assert.Equal(fixture.Job.Id, winner.JobId);
        Assert.Equal(PamRotationSource.Scheduled, winner.Source);
        Assert.Equal(fixture.Target.Id, winner.TargetSystemId);
        Assert.Equal(fixture.Target.Name, winner.TargetSystemName);
        Assert.Equal(PamTargetSystemKind.Mssql, winner.Kind);
        Assert.Equal(fixture.Target.PasswordPolicy, winner.PasswordPolicy);
        Assert.Equal(fixture.Cipher.Id, winner.CipherId);
        Assert.Equal(fixture.Config.AccountIdentity, winner.AccountIdentity);
        Assert.Equal(fixture.Config.TerminateSessions, winner.TerminateSessions);
        Assert.Equal(claimNow.Add(_releaseDelay), winner.ExecuteBy!.Value, LaxDateTimeComparer.Default);

        // The job records the winning claim, and ExecuteBy is the claimed-at instant plus the release delay.
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.Claimed, job!.Action);
        Assert.NotNull(job.ClaimedByAccessConnectorId);
        Assert.Equal(claimNow, job.ClaimedAt.Value, LaxDateTimeComparer.Default);
        Assert.Equal(
            job.ClaimedAt!.Value.Add(_releaseDelay), winner.ExecuteBy!.Value, LaxDateTimeComparer.Default);

        // AtMostOneInFlightAttemptPerJob: only the winner's attempt row was inserted.
        var details = Assert.Single(
            await pamRotationJobRepository.GetManyByConfigIdAsync(fixture.Config.Id, fixture.Now));
        var attempt = Assert.Single(details.Attempts);
        Assert.Equal(winner.AttemptId, attempt.Id);
        Assert.Equal(job.ClaimedByAccessConnectorId, attempt.ClaimedByAccessConnectorId);
        Assert.Equal(PamRotationAttemptAction.None, attempt.Action);
    }

    // Defense in depth: a forged cross-org assignment must not let the foreign access connector claim.
    [DatabaseTheory, DatabaseData]
    public async Task ClaimAsync_CrossOrganizationAccessConnectorWithForgedAssignment_NotEligibleAndZeroEffect(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var foreignOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var foreignAccessConnector = await CreateEnrolledAccessConnectorAsync(
            apiKeyRepository, pamAccessConnectorRepository, foreignOrganization.Id);
        // Forge the cross-org assignment directly at the repository layer.
        await AssignAsync(
            pamAccessConnectorRepository, foreignAccessConnector.Id, fixture.Target.Id, foreignOrganization.Id,
            fixture.Now);

        var result = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, foreignAccessConnector.Id, fixture.Now, _releaseDelay);

        Assert.Equal(PamRotationClaimOutcome.NotEligible, result.Outcome);
        Assert.Null(result.AttemptId);

        // Zero effect: the job is untouched and no attempt row exists.
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.None, job!.Action);
        Assert.Null(job.ClaimedByAccessConnectorId);
        Assert.Null(job.ClaimedAt);
        var details = Assert.Single(
            await pamRotationJobRepository.GetManyByConfigIdAsync(fixture.Config.Id, fixture.Now));
        Assert.Empty(details.Attempts);

        // The poll re-derives the same org join, so the foreign access connector never even sees the job.
        Assert.Empty(await pamRotationJobRepository.GetManyClaimableByAccessConnectorIdAsync(
            foreignAccessConnector.Id, fixture.Now));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ClaimAsync_UnassignedAccessConnector_NotEligible(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        // Same org, enrolled, but never assigned to the target.
        var unassigned = await CreateEnrolledAccessConnectorAsync(
            apiKeyRepository, pamAccessConnectorRepository, fixture.Organization.Id);

        var result = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, unassigned.Id, fixture.Now, _releaseDelay);

        Assert.Equal(PamRotationClaimOutcome.NotEligible, result.Outcome);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.None, job!.Action);
        Assert.Null(job.ClaimedByAccessConnectorId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ClaimAsync_BeforeNextClaimableAt_NotClaimable(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            nextClaimableAt: DateTime.UtcNow.AddHours(1));

        var result = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, fixture.Now, _releaseDelay);

        // The access connector is fully eligible; the job itself is just not claimable yet (still in backoff).
        Assert.Equal(PamRotationClaimOutcome.NotClaimable, result.Outcome);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.None, job!.Action);
        Assert.Null(job.ClaimedByAccessConnectorId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ClaimAsync_DisabledTargetOrPausedConfig_NotClaimable(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);

        // Config paused after offer: a transient hold (409), not the 404 for an unreachable job.
        fixture.Config.Enabled = false;
        await pamRotationConfigRepository.ReplaceAsync(fixture.Config);
        Assert.Equal(PamRotationClaimOutcome.NotClaimable,
            (await pamRotationJobRepository.ClaimAsync(
                fixture.Job.Id, fixture.AccessConnector.Id, fixture.Now, _releaseDelay))
            .Outcome);

        // Re-enable the config but disable the target: still held, for the same reason.
        fixture.Config.Enabled = true;
        await pamRotationConfigRepository.ReplaceAsync(fixture.Config);
        fixture.Target.Status = PamTargetSystemStatus.Disabled;
        await pamTargetSystemRepository.ReplaceAsync(fixture.Target);
        Assert.Equal(PamRotationClaimOutcome.NotClaimable,
            (await pamRotationJobRepository.ClaimAsync(
                fixture.Job.Id, fixture.AccessConnector.Id, fixture.Now, _releaseDelay))
            .Outcome);

        // Neither refusal touched the job or created an attempt.
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.None, job!.Action);
        Assert.Null(job.ClaimedByAccessConnectorId);
        var details = Assert.Single(
            await pamRotationJobRepository.GetManyByConfigIdAsync(fixture.Config.Id, fixture.Now));
        Assert.Empty(details.Attempts);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ClaimAsync_PastTheDeadlineBeforeAnySweep_NotClaimable(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: now.AddHours(-2), expiresAt: now.AddMinutes(-1));

        var result = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, now, _releaseDelay);

        // Already TimedOut by derivation, so neither claimable nor offered by the poll.
        Assert.Equal(PamRotationClaimOutcome.NotClaimable, result.Outcome);
        Assert.DoesNotContain(
            await pamRotationJobRepository.GetManyClaimableByAccessConnectorIdAsync(fixture.AccessConnector.Id, now),
            j => j.Id == fixture.Job.Id);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.None, job!.Action);
        Assert.Null(job.ClaimedByAccessConnectorId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task AcceptCipherWriteAsync_HappyPath_ReplacesCipherDataAndMarksAttempt(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, fixture.Now, _releaseDelay);
        Assert.Equal(PamRotationClaimOutcome.Claimed, claim.Outcome);
        var writeNow = fixture.Now.AddMinutes(1);
        const string rotatedData = "{\"rotatedSecret\":true}";

        var outcome = await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.AccessConnector.Id, rotatedData, fixture.Cipher.RevisionDate, writeNow);

        Assert.Equal(PamRotationCipherWriteOutcome.Accepted, outcome);

        // The cipher's Data was replaced and its RevisionDate bumped to the write time.
        var cipher = await cipherRepository.GetByIdAsync(fixture.Cipher.Id);
        Assert.NotNull(cipher);
        Assert.Equal(rotatedData, cipher!.Data);
        Assert.Equal(writeNow, cipher.RevisionDate, LaxDateTimeComparer.Default);

        // The attempt records the accepted write; it stays unreported until the success report.
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.True(attempt!.CipherUpdated);
        Assert.Equal(PamRotationAttemptAction.None, attempt.Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task AcceptCipherWriteAsync_WrongAccessConnector_RejectedAndNothingPersisted(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, fixture.Now, _releaseDelay);
        var impostor = await CreateEnrolledAccessConnectorAsync(
            apiKeyRepository, pamAccessConnectorRepository, fixture.Organization.Id);

        var outcome = await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, impostor.Id, "{\"stolen\":true}", fixture.Cipher.RevisionDate, fixture.Now);

        Assert.Equal(PamRotationCipherWriteOutcome.Rejected, outcome);
        var cipher = await cipherRepository.GetByIdAsync(fixture.Cipher.Id);
        Assert.Equal(fixture.Cipher.Data, cipher!.Data);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.False(attempt!.CipherUpdated);
    }

    [DatabaseTheory, DatabaseData]
    public async Task AcceptCipherWriteAsync_StaleLastKnownRevisionDate_RevisionMismatch(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, fixture.Now, _releaseDelay);

        // Drift beyond the 1-second tolerance simulates a concurrent user edit.
        var outcome = await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.AccessConnector.Id, "{\"rotated\":true}",
            fixture.Cipher.RevisionDate.AddSeconds(-5), fixture.Now);

        Assert.Equal(PamRotationCipherWriteOutcome.RevisionMismatch, outcome);
        var cipher = await cipherRepository.GetByIdAsync(fixture.Cipher.Id);
        Assert.Equal(fixture.Cipher.Data, cipher!.Data);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.False(attempt!.CipherUpdated);
    }

    // A drift wider than int.MaxValue overflows MSSQL's DATEDIFF, diverging from EF providers.
    [DatabaseTheory, DatabaseData]
    public async Task AcceptCipherWriteAsync_DriftBeyondDateDiffIntRange_RevisionMismatch(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, fixture.Now, _releaseDelay);

        var outcome = await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.AccessConnector.Id, "{\"rotated\":true}",
            fixture.Cipher.RevisionDate.AddDays(-60), fixture.Now);

        Assert.Equal(PamRotationCipherWriteOutcome.RevisionMismatch, outcome);
        var cipher = await cipherRepository.GetByIdAsync(fixture.Cipher.Id);
        Assert.Equal(fixture.Cipher.Data, cipher!.Data);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.False(attempt!.CipherUpdated);
    }

    // After the release sweep reclaims the job, the access connector's late write must be refused.
    [DatabaseTheory, DatabaseData]
    public async Task AcceptCipherWriteAsync_AfterJobReleased_Rejected(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.Add(-_releaseDelay).AddMinutes(-5); // Lease already expired by `now`.
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, claimTime, _releaseDelay);
        Assert.Equal(PamRotationClaimOutcome.Claimed, claim.Outcome);

        // The access connector never heartbeats, so by `now` it is stale and its expired lease is released.
        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);
        Assert.Contains(released, r => r.JobId == fixture.Job.Id);

        var outcome = await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.AccessConnector.Id, "{\"late\":true}", fixture.Cipher.RevisionDate, now);

        Assert.Equal(PamRotationCipherWriteOutcome.Rejected, outcome);
        var cipher = await cipherRepository.GetByIdAsync(fixture.Cipher.Id);
        Assert.Equal(fixture.Cipher.Data, cipher!.Data);
    }

    [DatabaseTheory, DatabaseData]
    public async Task MarkAttemptRotatedAsync_WithoutCipherUpdate_Rejected(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, fixture.Now, _releaseDelay);

        // VerifiedBeforeSuccess: a success report with no accepted cipher write cannot resolve the attempt.
        var outcome = await pamRotationJobRepository.MarkAttemptRotatedAsync(
            claim.AttemptId!.Value, fixture.AccessConnector.Id, PamSessionTerminationOutcome.NotRequested, fixture.Now);

        Assert.Equal(PamRotationAttemptResolveOutcome.Rejected, outcome);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.Equal(PamRotationAttemptAction.None, attempt!.Action);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.Claimed, job!.Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task MarkAttemptRotatedAsync_AfterAcceptedWrite_ResolvesAttemptAndSucceedsJob(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, fixture.Now, _releaseDelay);
        Assert.Equal(PamRotationCipherWriteOutcome.Accepted, await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.AccessConnector.Id, "{\"rotated\":true}", fixture.Cipher.RevisionDate,
            fixture.Now));
        var resolveNow = fixture.Now.AddMinutes(2);

        var outcome = await pamRotationJobRepository.MarkAttemptRotatedAsync(
            claim.AttemptId.Value, fixture.AccessConnector.Id, PamSessionTerminationOutcome.Terminated, resolveNow);

        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, outcome);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.Equal(PamRotationAttemptAction.Rotated, attempt!.Action);
        Assert.Equal(PamSessionTerminationOutcome.Terminated, attempt.SessionTermination);
        Assert.Equal(resolveNow, attempt.ResolvedDate.Value, LaxDateTimeComparer.Default);
        // The attempt keeps the executing access connector's identity permanently...
        Assert.Equal(fixture.AccessConnector.Id, attempt.ClaimedByAccessConnectorId);

        // ...while the job leaves Claimed with its claim fields nulled.
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.Succeeded, job!.Action);
        Assert.Null(job.ClaimedByAccessConnectorId);
        Assert.Null(job.ClaimedAt);
    }

    [DatabaseTheory, DatabaseData]
    public async Task MarkAttemptRotatedAsync_PastTheDeadline_Rejected(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.AddHours(-2);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime, expiresAt: now.AddMinutes(-1));
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, claimTime, _releaseDelay);
        Assert.Equal(PamRotationCipherWriteOutcome.Accepted, await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.AccessConnector.Id, "{\"rotated\":true}", fixture.Cipher.RevisionDate,
            claimTime));

        // The job derives as TimedOut before any sweep has run, so the late report is stale.
        var outcome = await pamRotationJobRepository.MarkAttemptRotatedAsync(
            claim.AttemptId.Value, fixture.AccessConnector.Id, PamSessionTerminationOutcome.NotRequested, now);

        Assert.Equal(PamRotationAttemptResolveOutcome.Rejected, outcome);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.Claimed, job!.Action);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.Equal(PamRotationAttemptAction.None, attempt!.Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task MarkAttemptErroredAsync_WithRetryBudget_RetriesJobWithBackoff(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, fixture.Now, _releaseDelay);
        var errorNow = fixture.Now.AddMinutes(1);
        var retryBaseDelay = TimeSpan.FromSeconds(60);

        var result = await pamRotationJobRepository.MarkAttemptErroredAsync(
            claim.AttemptId!.Value, fixture.AccessConnector.Id, "target unreachable",
            PamRotationSyncState.TargetUnchanged, errorNow, maxAttempts: 5, retryBaseDelay);

        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, result.Outcome);
        Assert.Equal(PamRotationJobStatus.Pending, result.JobStatus);
        Assert.Equal(1, result.ErroredAttemptCount);

        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.Equal(PamRotationAttemptAction.Errored, attempt!.Action);
        Assert.Equal("target unreachable", attempt.FailureReason);
        Assert.Equal(PamRotationSyncState.TargetUnchanged, attempt.SyncState);
        Assert.Equal(errorNow, attempt.ResolvedDate.Value, LaxDateTimeComparer.Default);

        // First backoff step: NextClaimableAt = now + retryBaseDelay * 2^(1-1).
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.None, job!.Action);
        Assert.Null(job.ClaimedByAccessConnectorId);
        Assert.Null(job.ClaimedAt);
        Assert.Equal(errorNow.Add(retryBaseDelay), job.NextClaimableAt, LaxDateTimeComparer.Default);
    }

    [DatabaseTheory, DatabaseData]
    public async Task MarkAttemptErroredAsync_BudgetExhausted_FailsJob(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, fixture.Now, _releaseDelay);

        // maxAttempts = 1: this first errored attempt already exhausts the budget.
        var result = await pamRotationJobRepository.MarkAttemptErroredAsync(
            claim.AttemptId!.Value, fixture.AccessConnector.Id, "still unreachable", PamRotationSyncState.Indeterminate,
            fixture.Now, maxAttempts: 1, TimeSpan.FromSeconds(60));

        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, result.Outcome);
        Assert.Equal(PamRotationJobStatus.Failed, result.JobStatus);
        Assert.Equal(1, result.ErroredAttemptCount);

        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.Failed, job!.Action);
        Assert.Null(job.ClaimedByAccessConnectorId);
        Assert.Null(job.ClaimedAt);
    }

    // Abandoned attempts are never charged against the retry budget.
    [DatabaseTheory, DatabaseData]
    public async Task MarkAttemptErroredAsync_AbandonedAttemptsNotCounted(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var firstClaimTime = now.Add(-_releaseDelay).AddMinutes(-5);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: firstClaimTime);

        // First claim goes stale and is released -> its attempt derives as Abandoned.
        var firstClaim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, firstClaimTime, _releaseDelay);
        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);
        Assert.Contains(released, r => r.JobId == fixture.Job.Id);
        var afterRelease = Assert.Single(await pamRotationJobRepository.GetManyByConfigIdAsync(fixture.Config.Id, now));
        var abandoned = Assert.Single(afterRelease.Attempts);
        Assert.Equal(firstClaim.AttemptId!.Value, abandoned.Id);
        Assert.Equal(PamRotationAttemptStatus.Abandoned, abandoned.Status);

        // Errored count is 1 (abandoned isn't charged), so retry, not failure.
        var secondClaim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, now, _releaseDelay);
        Assert.Equal(PamRotationClaimOutcome.Claimed, secondClaim.Outcome);
        var result = await pamRotationJobRepository.MarkAttemptErroredAsync(
            secondClaim.AttemptId!.Value, fixture.AccessConnector.Id, "flaky target",
            PamRotationSyncState.TargetUnchanged, now, maxAttempts: 2, TimeSpan.FromSeconds(60));

        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, result.Outcome);
        Assert.Equal(PamRotationJobStatus.Pending, result.JobStatus);
        Assert.Equal(1, result.ErroredAttemptCount);
    }

    // Released and re-claimed by the same access connector: the old attempt is unreported but no longer the claim's.
    [DatabaseTheory, DatabaseData]
    public async Task MarkAttemptErroredAsync_AttemptFromAnEarlierClaim_Rejected(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var firstClaimTime = now.Add(-_releaseDelay).AddMinutes(-5);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: firstClaimTime);
        var firstClaim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, firstClaimTime, _releaseDelay);
        Assert.Contains(await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay),
            r => r.JobId == fixture.Job.Id);
        Assert.Equal(PamRotationClaimOutcome.Claimed,
            (await pamRotationJobRepository.ClaimAsync(
                fixture.Job.Id, fixture.AccessConnector.Id, now, _releaseDelay))
            .Outcome);

        var result = await pamRotationJobRepository.MarkAttemptErroredAsync(
            firstClaim.AttemptId!.Value, fixture.AccessConnector.Id, "late report",
            PamRotationSyncState.TargetUnchanged, now, maxAttempts: 5, TimeSpan.FromSeconds(60));

        Assert.Equal(PamRotationAttemptResolveOutcome.Rejected, result.Outcome);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.Claimed, job!.Action);
        var stale = await pamRotationJobRepository.GetAttemptByIdAsync(firstClaim.AttemptId.Value);
        Assert.Equal(PamRotationAttemptAction.None, stale!.Action);
        Assert.Null(stale.FailureReason);
    }

    [DatabaseTheory, DatabaseData]
    public async Task TimeoutDueAsync_PendingJobPastExpiry_TimedOutAsUnroutable(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: now.AddHours(-2), expiresAt: now.AddMinutes(-1));

        var timedOut = await pamRotationJobRepository.TimeoutDueAsync(now);

        // The sweep is set-based across the whole table, so scope the assertion to this test's job.
        var row = Assert.Single(timedOut, r => r.JobId == fixture.Job.Id);
        Assert.Equal(fixture.Config.Id, row.RotationConfigId);
        Assert.Equal(fixture.Organization.Id, row.OrganizationId);
        Assert.Equal(fixture.Cipher.Id, row.CipherId);
        Assert.Equal(PamRotationSource.Scheduled, row.Source);
        // Never claimed: unroutable, not stuck.
        Assert.Null(row.ClaimedByAccessConnectorId);
        Assert.Equal(0, row.AttemptCount);

        // Nothing is written to the job; the read derives TimedOut from ExpiresAt alone.
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.None, job!.Action);
        var details = Assert.Single(await pamRotationJobRepository.GetManyByConfigIdAsync(fixture.Config.Id, now));
        Assert.Equal(PamRotationJobStatus.TimedOut, details.Status);
    }

    // The journal, not a status flip, is what keeps the audit event and reschedule to a single firing.
    [DatabaseTheory, DatabaseData]
    public async Task TimeoutDueAsync_SecondRun_DoesNotReturnAlreadyRecordedJob(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: now.AddHours(-2), expiresAt: now.AddMinutes(-1));
        await pamRotationJobRepository.TimeoutDueAsync(now);

        var secondRun = await pamRotationJobRepository.TimeoutDueAsync(now.AddMinutes(1));

        Assert.DoesNotContain(secondRun, r => r.JobId == fixture.Job.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task TimeoutDueAsync_ClaimedJobPastExpiry_RecordsTimeoutAndLeavesTheClaimStored(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.AddHours(-2);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime, expiresAt: now.AddMinutes(-1));
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, claimTime, _releaseDelay);
        Assert.Equal(PamRotationClaimOutcome.Claimed, claim.Outcome);

        var timedOut = await pamRotationJobRepository.TimeoutDueAsync(now);

        var row = Assert.Single(timedOut, r => r.JobId == fixture.Job.Id);
        // Claimed at timeout: stuck, not unroutable.
        Assert.Equal(fixture.AccessConnector.Id, row.ClaimedByAccessConnectorId);
        Assert.Equal(1, row.AttemptCount);

        // The stored claim is left as it was...
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.Claimed, job!.Action);
        Assert.Equal(fixture.AccessConnector.Id, job.ClaimedByAccessConnectorId);

        // ...while the read derives TimedOut, hides the claim, and ends the attempt at the deadline.
        var details = Assert.Single(await pamRotationJobRepository.GetManyByConfigIdAsync(fixture.Config.Id, now));
        Assert.Equal(PamRotationJobStatus.TimedOut, details.Status);
        Assert.Null(details.ClaimedByAccessConnectorId);
        Assert.Null(details.ClaimedAt);
        var attempt = Assert.Single(details.Attempts);
        Assert.Equal(claim.AttemptId!.Value, attempt.Id);
        Assert.Equal(PamRotationAttemptStatus.Abandoned, attempt.Status);
        Assert.Equal(fixture.Job.ExpiresAt, attempt.ResolvedDate!.Value, LaxDateTimeComparer.Default);
    }

    // Success wins: a Rotated attempt's job is never timed out, no matter its ExpiresAt.
    [DatabaseTheory, DatabaseData]
    public async Task TimeoutDueAsync_JobWithRotatedAttempt_NotTimedOut(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.AddHours(-2);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime, expiresAt: now.AddMinutes(-1));
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, claimTime, _releaseDelay);
        Assert.Equal(PamRotationCipherWriteOutcome.Accepted, await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.AccessConnector.Id, "{\"rotated\":true}", fixture.Cipher.RevisionDate,
            claimTime));
        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, await pamRotationJobRepository.MarkAttemptRotatedAsync(
            claim.AttemptId.Value, fixture.AccessConnector.Id, PamSessionTerminationOutcome.NotRequested, claimTime));

        var timedOut = await pamRotationJobRepository.TimeoutDueAsync(now);

        Assert.DoesNotContain(timedOut, r => r.JobId == fixture.Job.Id);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.Succeeded, job!.Action);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.Equal(PamRotationAttemptAction.Rotated, attempt!.Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReleaseExpiredLeasesAsync_StaleAccessConnectorPastExecuteBy_ReleasesJobAndAbandonsAttempt(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.Add(-_releaseDelay).AddMinutes(-5); // ExecuteBy = claimTime + releaseDelay < now.
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, claimTime, _releaseDelay);
        Assert.Equal(PamRotationClaimOutcome.Claimed, claim.Outcome);
        // The access connector never heartbeats, so it is stale (LastHeartbeatAt null).

        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);

        var row = Assert.Single(released, r => r.JobId == fixture.Job.Id);
        Assert.Equal(fixture.Config.Id, row.RotationConfigId);
        Assert.Equal(fixture.Organization.Id, row.OrganizationId);
        Assert.Equal(fixture.Cipher.Id, row.CipherId);
        // The pre-clear claimant survives on the audit row despite the job's own field clearing.
        Assert.Equal(fixture.AccessConnector.Id, row.ClaimedByAccessConnectorId);

        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.None, job!.Action);
        Assert.Null(job.ClaimedByAccessConnectorId);
        Assert.Null(job.ClaimedAt);
        // Re-claimable exactly at the lease's end (pre-clear ClaimedAt + releaseDelay), not at the sweep's run time.
        Assert.Equal(claimTime.Add(_releaseDelay), job.NextClaimableAt, LaxDateTimeComparer.Default);

        // Abandoned is derived; the release records only when the attempt ended.
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId!.Value);
        Assert.Equal(PamRotationAttemptAction.None, attempt!.Action);
        Assert.Equal(now, attempt.ResolvedDate.Value, LaxDateTimeComparer.Default);
        var details = Assert.Single(await pamRotationJobRepository.GetManyByConfigIdAsync(fixture.Config.Id, now));
        Assert.Equal(PamRotationAttemptStatus.Abandoned, Assert.Single(details.Attempts).Status);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReleaseExpiredLeasesAsync_FreshHeartbeat_NotReleased(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.Add(-_releaseDelay).AddMinutes(-5); // Lease expired...
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, claimTime, _releaseDelay);
        // Slow, not gone: a fresh heartbeat keeps the claim alive.
        await pamAccessConnectorRepository.UpdateHeartbeatAsync(
            fixture.AccessConnector.Id, now, TimeSpan.FromSeconds(15));

        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);

        Assert.DoesNotContain(released, r => r.JobId == fixture.Job.Id);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.Claimed, job!.Action);
        Assert.Equal(fixture.AccessConnector.Id, job.ClaimedByAccessConnectorId);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId!.Value);
        Assert.Equal(PamRotationAttemptAction.None, attempt!.Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReleaseExpiredLeasesAsync_WithinLease_NotReleased(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: now);
        // Release fires at lease expiry, never merely at stale-heartbeat detection.
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, now, _releaseDelay);

        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);

        Assert.DoesNotContain(released, r => r.JobId == fixture.Job.Id);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.Claimed, job!.Action);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId!.Value);
        Assert.Equal(PamRotationAttemptAction.None, attempt!.Action);
    }

    // Success wins on the release path too: a Rotated attempt's job is never released.
    [DatabaseTheory, DatabaseData]
    public async Task ReleaseExpiredLeasesAsync_JobWithRotatedAttempt_NotReleased(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.Add(-_releaseDelay).AddMinutes(-5);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, claimTime, _releaseDelay);
        Assert.Equal(PamRotationCipherWriteOutcome.Accepted, await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.AccessConnector.Id, "{\"rotated\":true}", fixture.Cipher.RevisionDate,
            claimTime));
        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, await pamRotationJobRepository.MarkAttemptRotatedAsync(
            claim.AttemptId.Value, fixture.AccessConnector.Id, PamSessionTerminationOutcome.NotRequested, claimTime));

        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);

        Assert.DoesNotContain(released, r => r.JobId == fixture.Job.Id);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.Succeeded, job!.Action);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.Equal(PamRotationAttemptAction.Rotated, attempt!.Action);
    }

    // A claim past the job's deadline is the timeout sweep's to record, not a release.
    [DatabaseTheory, DatabaseData]
    public async Task ReleaseExpiredLeasesAsync_TimedOutClaim_NotReleased(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.AddHours(-2);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime, expiresAt: now.AddMinutes(-1));
        Assert.Equal(PamRotationClaimOutcome.Claimed,
            (await pamRotationJobRepository.ClaimAsync(
                fixture.Job.Id, fixture.AccessConnector.Id, claimTime, _releaseDelay))
            .Outcome);

        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);

        Assert.DoesNotContain(released, r => r.JobId == fixture.Job.Id);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobAction.Claimed, job!.Action);
        Assert.Equal(fixture.AccessConnector.Id, job.ClaimedByAccessConnectorId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyRecentByAccessConnectorIdAsync_JobWorkedByTwoAccessConnectors_EachSeesOnlyItsOwnAttempt(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var firstClaim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, fixture.Now, _releaseDelay);
        // Errored with budget left: the job goes back to Pending and its claim fields are cleared.
        var errored = await pamRotationJobRepository.MarkAttemptErroredAsync(
            firstClaim.AttemptId!.Value, fixture.AccessConnector.Id, "target unreachable",
            PamRotationSyncState.TargetUnchanged, fixture.Now, maxAttempts: 5, TimeSpan.Zero);
        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, errored.Outcome);
        Assert.Equal(PamRotationJobStatus.Pending, errored.JobStatus);

        var second = await CreateEnrolledAccessConnectorAsync(
            apiKeyRepository, pamAccessConnectorRepository, fixture.Organization.Id);
        await AssignAsync(
            pamAccessConnectorRepository, second.Id, fixture.Target.Id, fixture.Organization.Id, fixture.Now);
        var retake = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, second.Id, fixture.Now.AddMinutes(1), _releaseDelay);
        Assert.NotNull(retake.AttemptId);

        var readNow = fixture.Now.AddMinutes(2);
        var forFirst = await pamRotationJobRepository.GetManyRecentByAccessConnectorIdAsync(
            fixture.AccessConnector.Id, 10, readNow);

        var firstJob = Assert.Single(forFirst);
        Assert.Equal(fixture.Job.Id, firstJob.Id);
        Assert.Equal(second.Id, firstJob.ClaimedByAccessConnectorId);
        var firstAttempt = Assert.Single(firstJob.Attempts);
        Assert.Equal(firstClaim.AttemptId.Value, firstAttempt.Id);
        Assert.Equal(PamRotationAttemptStatus.Errored, firstAttempt.Status);

        var forSecond = await pamRotationJobRepository.GetManyRecentByAccessConnectorIdAsync(second.Id, 10, readNow);
        var secondJob = Assert.Single(forSecond);
        Assert.Equal(fixture.Job.Id, secondJob.Id);
        var secondAttempt = Assert.Single(secondJob.Attempts);
        Assert.Equal(retake.AttemptId.Value, secondAttempt.Id);
        Assert.Equal(PamRotationAttemptStatus.Executing, secondAttempt.Status);

        var idle = await CreateEnrolledAccessConnectorAsync(
            apiKeyRepository, pamAccessConnectorRepository, fixture.Organization.Id);
        Assert.Empty(await pamRotationJobRepository.GetManyRecentByAccessConnectorIdAsync(idle.Id, 10, readNow));
    }

    // The access connector read only carries that connector's attempts, so its old attempt is the newest one in the
    // list even after another access connector re-claims the job. It must not read as Executing.
    [DatabaseTheory, DatabaseData]
    public async Task GetManyRecentByAccessConnectorIdAsync_ReleasedThenReclaimedByAnother_OldAttemptReadsAbandoned(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var firstClaimTime = now.Add(-_releaseDelay).AddMinutes(-5);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: firstClaimTime);
        var firstClaim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.AccessConnector.Id, firstClaimTime, _releaseDelay);
        Assert.Contains(await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay),
            r => r.JobId == fixture.Job.Id);
        var second = await CreateEnrolledAccessConnectorAsync(
            apiKeyRepository, pamAccessConnectorRepository, fixture.Organization.Id);
        await AssignAsync(pamAccessConnectorRepository, second.Id, fixture.Target.Id, fixture.Organization.Id, now);
        Assert.Equal(PamRotationClaimOutcome.Claimed,
            (await pamRotationJobRepository.ClaimAsync(fixture.Job.Id, second.Id, now, _releaseDelay)).Outcome);

        var forFirst = Assert.Single(
            await pamRotationJobRepository.GetManyRecentByAccessConnectorIdAsync(fixture.AccessConnector.Id, 10, now));

        Assert.Equal(PamRotationJobStatus.Claimed, forFirst.Status);
        var attempt = Assert.Single(forFirst.Attempts);
        Assert.Equal(firstClaim.AttemptId!.Value, attempt.Id);
        Assert.Equal(PamRotationAttemptStatus.Abandoned, attempt.Status);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyRecentByAccessConnectorIdAsync_ReturnsNewestFirstAndHonoursTheLimit(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var older = now.AddHours(-2);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamAccessConnectorRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: older);
        await pamRotationJobRepository.ClaimAsync(fixture.Job.Id, fixture.AccessConnector.Id, older, _releaseDelay);

        // AtMostOneActiveJobPerConfig is per-config; the access connector can still work a second config on the same
        // target.
        var newerCipher = await CreateCipherAsync(cipherRepository, fixture.Organization.Id);
        var newerConfig = await pamRotationConfigRepository.CreateAsync(
            BuildConfig(fixture.Organization.Id, newerCipher.Id, fixture.Target.Id, now));
        var newerJob = BuildPendingJob(newerConfig.Id, now);
        Assert.Equal(PamRotationJobCreateOutcome.Created, await pamRotationJobRepository.CreateGuardedAsync(newerJob));
        await pamRotationJobRepository.ClaimAsync(newerJob.Id, fixture.AccessConnector.Id, now, _releaseDelay);

        var all = await pamRotationJobRepository.GetManyRecentByAccessConnectorIdAsync(
            fixture.AccessConnector.Id, 10, now);
        Assert.Equal(new[] { newerJob.Id, fixture.Job.Id }, all.Select(job => job.Id).ToArray());

        // The cap keeps the newest, not whatever the storage engine returns first.
        var capped = await pamRotationJobRepository.GetManyRecentByAccessConnectorIdAsync(
            fixture.AccessConnector.Id, 1, now);
        Assert.Equal(newerJob.Id, Assert.Single(capped).Id);
    }

    private sealed record ClaimableJobFixture(
        Organization Organization,
        PamTargetSystem Target,
        PamAccessConnector AccessConnector,
        Cipher Cipher,
        PamRotationConfig Config,
        PamRotationJob Job,
        DateTime Now);

    /// <summary>
    /// Seeds the full eligibility graph for a claimable job: org, target, access connector, cipher, config, and job.
    /// <paramref name="now"/> lets sweep tests place the graph in the past; defaults keep the job claimable now.
    /// </summary>
    private static async Task<ClaimableJobFixture> SeedClaimableJobAsync(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamAccessConnectorRepository pamAccessConnectorRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository,
        DateTime? now = null,
        DateTime? expiresAt = null,
        DateTime? nextClaimableAt = null)
    {
        var seedNow = now ?? DateTime.UtcNow;
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var target = await CreateAutomaticTargetAsync(pamTargetSystemRepository, organization.Id, seedNow);
        var accessConnector = await CreateEnrolledAccessConnectorAsync(
            apiKeyRepository, pamAccessConnectorRepository, organization.Id);
        await AssignAsync(pamAccessConnectorRepository, accessConnector.Id, target.Id, organization.Id, seedNow);
        var cipher = await CreateCipherAsync(cipherRepository, organization.Id);
        var config = await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, cipher.Id, target.Id, seedNow));

        var job = BuildPendingJob(config.Id, seedNow, expiresAt, nextClaimableAt);
        Assert.Equal(PamRotationJobCreateOutcome.Created, await pamRotationJobRepository.CreateGuardedAsync(job));

        return new ClaimableJobFixture(organization, target, accessConnector, cipher, config, job, seedNow);
    }

    private static async Task<PamTargetSystem> CreateAutomaticTargetAsync(
        IPamTargetSystemRepository pamTargetSystemRepository, Guid organizationId, DateTime now)
        => await pamTargetSystemRepository.CreateAsync(new PamTargetSystem
        {
            OrganizationId = organizationId,
            Name = $"target-{Guid.NewGuid()}",
            Method = PamTargetSystemMethod.Automatic,
            Kind = PamTargetSystemKind.Mssql,
            PasswordPolicy = """{"minLength":16,"maxLength":32}""",
            SupportsSessionTermination = true,
            Status = PamTargetSystemStatus.Active,
            CreationDate = now,
            RevisionDate = now,
        });

    private static async Task<PamAccessConnector> CreateEnrolledAccessConnectorAsync(
        IApiKeyRepository apiKeyRepository, IPamAccessConnectorRepository pamAccessConnectorRepository,
        Guid organizationId)
    {
        var apiKey = await apiKeyRepository.CreateAsync(new ApiKey
        {
            ServiceAccountId = null,
            Name = $"access-connector-{Guid.NewGuid()}",
            Scope = """["api.pam.rotation"]""",
            EncryptedPayload = "encrypted-payload",
            Key = "encrypted-key",
        });
        return await pamAccessConnectorRepository.CreateAsync(new PamAccessConnector
        {
            OrganizationId = organizationId,
            Name = $"access-connector-{Guid.NewGuid()}",
            ApiKeyId = apiKey.Id,
            Status = PamAccessConnectorStatus.Enabled,
        });
    }

    private static async Task AssignAsync(
        IPamAccessConnectorRepository pamAccessConnectorRepository, Guid accessConnectorId, Guid targetSystemId,
        Guid organizationId, DateTime now)
        => await pamAccessConnectorRepository.CreateAssignmentAsync(new PamAccessConnectorTargetAssignment
        {
            Id = CombGuid.Generate(),
            AccessConnectorId = accessConnectorId,
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

    private static PamRotationConfig BuildConfig(
        Guid organizationId, Guid cipherId, Guid targetSystemId, DateTime now, bool enabled = true) => new()
        {
            OrganizationId = organizationId,
            CipherId = cipherId,
            TargetSystemId = targetSystemId,
            AccountIdentity = "svc-account",
            TerminateSessions = true,
            RotateOnAccessEnd = false,
            Enabled = enabled,
            CreationDate = now,
            RevisionDate = now,
        };

    // Defaults far into the future so a concurrent timeout-sweep test can't time this job out.
    private static PamRotationJob BuildPendingJob(
        Guid configId, DateTime now, DateTime? expiresAt = null, DateTime? nextClaimableAt = null) => new()
        {
            Id = CombGuid.Generate(),
            RotationConfigId = configId,
            Source = PamRotationSource.Scheduled,
            Action = PamRotationJobAction.None,
            CreationDate = now,
            NextClaimableAt = nextClaimableAt ?? now,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(1),
        };
}

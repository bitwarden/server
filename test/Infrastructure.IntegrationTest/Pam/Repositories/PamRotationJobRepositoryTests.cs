using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
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
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);

        // The seed already created a Pending job for the config -- a second offer must be refused as ActiveJobExists,
        // and only the first job may exist.
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

    // First-claim-wins under real contention: two daemons race the same Pending job on concurrent connections.
    // Exactly one wins with the full work snapshot; the loser sees NotClaimable (the job left Pending), and only the
    // winner's Executing attempt exists.
    [DatabaseTheory, DatabaseData]
    public async Task ClaimAsync_ConcurrentDoubleClaim_ExactlyOneWinner(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var rival = await CreateEnrolledDaemonAsync(apiKeyRepository, pamDaemonRepository, fixture.Organization.Id);
        await AssignAsync(pamDaemonRepository, rival.Id, fixture.Target.Id, fixture.Organization.Id, fixture.Now);
        var claimNow = fixture.Now;

        var results = await Task.WhenAll(
            pamRotationJobRepository.ClaimAsync(fixture.Job.Id, fixture.Daemon.Id, claimNow, _releaseDelay),
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
        Assert.Equal(claimNow.Add(_releaseDelay), winner.ExecuteBy);

        // The job records the winning claim, and ExecuteBy is exactly ClaimedAt + releaseDelay.
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Claimed, job!.Status);
        Assert.NotNull(job.ClaimedByDaemonId);
        Assert.Equal(claimNow, job.ClaimedAt);
        Assert.Equal(job.ClaimedAt!.Value.Add(_releaseDelay), winner.ExecuteBy);

        // AtMostOneInFlightAttemptPerJob: only the winner's attempt row was inserted.
        var details = Assert.Single(await pamRotationJobRepository.GetManyByConfigIdAsync(fixture.Config.Id));
        var attempt = Assert.Single(details.Attempts);
        Assert.Equal(winner.AttemptId, attempt.Id);
        Assert.Equal(job.ClaimedByDaemonId, attempt.ClaimedByDaemonId);
        Assert.Equal(PamRotationAttemptStatus.Executing, attempt.Status);
    }

    // The claim sproc's Daemon.OrganizationId = Config.OrganizationId join is defense in depth: even a forged
    // assignment row linking an org-B daemon to an org-A target (the assignment FKs do not enforce same-org) must not
    // let the foreign daemon claim -- and the poll must not surface the job to it either.
    [DatabaseTheory, DatabaseData]
    public async Task ClaimAsync_CrossOrganizationDaemonWithForgedAssignment_NotEligibleAndZeroEffect(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var foreignOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var foreignDaemon = await CreateEnrolledDaemonAsync(
            apiKeyRepository, pamDaemonRepository, foreignOrganization.Id);
        // Forge the cross-org assignment directly at the repository layer.
        await AssignAsync(
            pamDaemonRepository, foreignDaemon.Id, fixture.Target.Id, foreignOrganization.Id, fixture.Now);

        var result = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, foreignDaemon.Id, fixture.Now, _releaseDelay);

        Assert.Equal(PamRotationClaimOutcome.NotEligible, result.Outcome);
        Assert.Null(result.AttemptId);

        // Zero effect: the job is untouched and no attempt row exists.
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Pending, job!.Status);
        Assert.Null(job.ClaimedByDaemonId);
        Assert.Null(job.ClaimedAt);
        var details = Assert.Single(await pamRotationJobRepository.GetManyByConfigIdAsync(fixture.Config.Id));
        Assert.Empty(details.Attempts);

        // The poll re-derives the same org join, so the foreign daemon never even sees the job.
        Assert.Empty(await pamRotationJobRepository.GetManyClaimableByDaemonIdAsync(foreignDaemon.Id, fixture.Now));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ClaimAsync_UnassignedDaemon_NotEligible(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        // Same org, enrolled, but never assigned to the target.
        var unassigned = await CreateEnrolledDaemonAsync(
            apiKeyRepository, pamDaemonRepository, fixture.Organization.Id);

        var result = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, unassigned.Id, fixture.Now, _releaseDelay);

        Assert.Equal(PamRotationClaimOutcome.NotEligible, result.Outcome);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Pending, job!.Status);
        Assert.Null(job.ClaimedByDaemonId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ClaimAsync_BeforeNextClaimableAt_NotClaimable(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            nextClaimableAt: DateTime.UtcNow.AddHours(1));

        var result = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, fixture.Now, _releaseDelay);

        // The daemon is fully eligible; the job itself is just not claimable yet (still in backoff).
        Assert.Equal(PamRotationClaimOutcome.NotClaimable, result.Outcome);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Pending, job!.Status);
        Assert.Null(job.ClaimedByDaemonId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ClaimAsync_DisabledTargetOrPausedConfig_NotEligible(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);

        // Pause the config after the job was offered: EligibleClaimsOnly's capability half fails.
        fixture.Config.Enabled = false;
        await pamRotationConfigRepository.ReplaceAsync(fixture.Config);
        Assert.Equal(PamRotationClaimOutcome.NotEligible,
            (await pamRotationJobRepository.ClaimAsync(fixture.Job.Id, fixture.Daemon.Id, fixture.Now, _releaseDelay))
            .Outcome);

        // Re-enable the config but disable the target: still not eligible.
        fixture.Config.Enabled = true;
        await pamRotationConfigRepository.ReplaceAsync(fixture.Config);
        fixture.Target.Status = PamTargetSystemStatus.Disabled;
        await pamTargetSystemRepository.ReplaceAsync(fixture.Target);
        Assert.Equal(PamRotationClaimOutcome.NotEligible,
            (await pamRotationJobRepository.ClaimAsync(fixture.Job.Id, fixture.Daemon.Id, fixture.Now, _releaseDelay))
            .Outcome);

        // Neither refusal touched the job or created an attempt.
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Pending, job!.Status);
        Assert.Null(job.ClaimedByDaemonId);
        var details = Assert.Single(await pamRotationJobRepository.GetManyByConfigIdAsync(fixture.Config.Id));
        Assert.Empty(details.Attempts);
    }

    [DatabaseTheory, DatabaseData]
    public async Task AcceptCipherWriteAsync_HappyPath_ReplacesCipherDataAndMarksAttempt(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, fixture.Now, _releaseDelay);
        Assert.Equal(PamRotationClaimOutcome.Claimed, claim.Outcome);
        var writeNow = fixture.Now.AddMinutes(1);
        const string rotatedData = "{\"rotatedSecret\":true}";

        var outcome = await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.Daemon.Id, rotatedData, fixture.Cipher.RevisionDate, writeNow);

        Assert.Equal(PamRotationCipherWriteOutcome.Accepted, outcome);

        // The cipher's Data was replaced and its RevisionDate bumped to the write time.
        var cipher = await cipherRepository.GetByIdAsync(fixture.Cipher.Id);
        Assert.NotNull(cipher);
        Assert.Equal(rotatedData, cipher!.Data);
        Assert.Equal(writeNow, cipher.RevisionDate);

        // The attempt records the accepted write; it stays Executing until the success report.
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.True(attempt!.CipherUpdated);
        Assert.Equal(PamRotationAttemptStatus.Executing, attempt.Status);
    }

    [DatabaseTheory, DatabaseData]
    public async Task AcceptCipherWriteAsync_WrongDaemon_RejectedAndNothingPersisted(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, fixture.Now, _releaseDelay);
        var impostor = await CreateEnrolledDaemonAsync(
            apiKeyRepository, pamDaemonRepository, fixture.Organization.Id);

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
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, fixture.Now, _releaseDelay);

        // Drift beyond the 1-second tolerance: a concurrent user edit would have bumped the revision date since the
        // daemon last read the cipher.
        var outcome = await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.Daemon.Id, "{\"rotated\":true}",
            fixture.Cipher.RevisionDate.AddSeconds(-5), fixture.Now);

        Assert.Equal(PamRotationCipherWriteOutcome.RevisionMismatch, outcome);
        var cipher = await cipherRepository.GetByIdAsync(fixture.Cipher.Id);
        Assert.Equal(fixture.Cipher.Data, cipher!.Data);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.False(attempt!.CipherUpdated);
    }

    // The release-sweep vs cipher-write interleaving: once the sweep has released the job (status back to Pending,
    // attempt Abandoned), the daemon's late write must be refused -- the atomic accept sproc re-verifies the claim
    // under the same job-row lock the sweep takes.
    [DatabaseTheory, DatabaseData]
    public async Task AcceptCipherWriteAsync_AfterJobReleased_Rejected(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.Add(-_releaseDelay).AddMinutes(-5); // Lease already expired by `now`.
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, claimTime, _releaseDelay);
        Assert.Equal(PamRotationClaimOutcome.Claimed, claim.Outcome);

        // The daemon never heartbeats, so by `now` it is stale and its expired lease is released.
        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);
        Assert.Contains(released, r => r.JobId == fixture.Job.Id);

        var outcome = await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.Daemon.Id, "{\"late\":true}", fixture.Cipher.RevisionDate, now);

        Assert.Equal(PamRotationCipherWriteOutcome.Rejected, outcome);
        var cipher = await cipherRepository.GetByIdAsync(fixture.Cipher.Id);
        Assert.Equal(fixture.Cipher.Data, cipher!.Data);
    }

    [DatabaseTheory, DatabaseData]
    public async Task MarkAttemptRotatedAsync_WithoutCipherUpdate_Rejected(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, fixture.Now, _releaseDelay);

        // VerifiedBeforeSuccess: a success report with no accepted cipher write cannot resolve the attempt.
        var outcome = await pamRotationJobRepository.MarkAttemptRotatedAsync(
            claim.AttemptId!.Value, fixture.Daemon.Id, PamSessionTerminationOutcome.NotRequested, fixture.Now);

        Assert.Equal(PamRotationAttemptResolveOutcome.Rejected, outcome);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.Equal(PamRotationAttemptStatus.Executing, attempt!.Status);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Claimed, job!.Status);
    }

    [DatabaseTheory, DatabaseData]
    public async Task MarkAttemptRotatedAsync_AfterAcceptedWrite_ResolvesAttemptAndSucceedsJob(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, fixture.Now, _releaseDelay);
        Assert.Equal(PamRotationCipherWriteOutcome.Accepted, await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.Daemon.Id, "{\"rotated\":true}", fixture.Cipher.RevisionDate, fixture.Now));
        var resolveNow = fixture.Now.AddMinutes(2);

        var outcome = await pamRotationJobRepository.MarkAttemptRotatedAsync(
            claim.AttemptId.Value, fixture.Daemon.Id, PamSessionTerminationOutcome.Terminated, resolveNow);

        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, outcome);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.Equal(PamRotationAttemptStatus.Rotated, attempt!.Status);
        Assert.Equal(PamSessionTerminationOutcome.Terminated, attempt.SessionTermination);
        Assert.Equal(resolveNow, attempt.ResolvedDate);
        // The attempt keeps the executing daemon's identity permanently...
        Assert.Equal(fixture.Daemon.Id, attempt.ClaimedByDaemonId);

        // ...while the job leaves Claimed with its claim fields nulled.
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Succeeded, job!.Status);
        Assert.Null(job.ClaimedByDaemonId);
        Assert.Null(job.ClaimedAt);
    }

    [DatabaseTheory, DatabaseData]
    public async Task MarkAttemptErroredAsync_WithRetryBudget_RetriesJobWithBackoff(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, fixture.Now, _releaseDelay);
        var errorNow = fixture.Now.AddMinutes(1);
        var retryBaseDelay = TimeSpan.FromSeconds(60);

        var result = await pamRotationJobRepository.MarkAttemptErroredAsync(
            claim.AttemptId!.Value, fixture.Daemon.Id, "target unreachable", PamRotationSyncState.TargetUnchanged,
            errorNow, maxAttempts: 5, retryBaseDelay);

        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, result.Outcome);
        Assert.Equal(PamRotationJobStatus.Pending, result.JobStatus);
        Assert.Equal(1, result.ErroredAttemptCount);

        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.Equal(PamRotationAttemptStatus.Errored, attempt!.Status);
        Assert.Equal("target unreachable", attempt.FailureReason);
        Assert.Equal(PamRotationSyncState.TargetUnchanged, attempt.SyncState);
        Assert.Equal(errorNow, attempt.ResolvedDate);

        // The job goes back to Pending with the claim fields cleared and the first backoff step applied:
        // NextClaimableAt = now + retryBaseDelay * 2^(1-1).
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Pending, job!.Status);
        Assert.Null(job.ClaimedByDaemonId);
        Assert.Null(job.ClaimedAt);
        Assert.Equal(errorNow.Add(retryBaseDelay), job.NextClaimableAt);
    }

    [DatabaseTheory, DatabaseData]
    public async Task MarkAttemptErroredAsync_BudgetExhausted_FailsJob(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, fixture.Now, _releaseDelay);

        // maxAttempts = 1: this first errored attempt already exhausts the budget.
        var result = await pamRotationJobRepository.MarkAttemptErroredAsync(
            claim.AttemptId!.Value, fixture.Daemon.Id, "still unreachable", PamRotationSyncState.Indeterminate,
            fixture.Now, maxAttempts: 1, TimeSpan.FromSeconds(60));

        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, result.Outcome);
        Assert.Equal(PamRotationJobStatus.Failed, result.JobStatus);
        Assert.Equal(1, result.ErroredAttemptCount);

        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Failed, job!.Status);
        Assert.Null(job.ClaimedByDaemonId);
        Assert.Null(job.ClaimedAt);
    }

    // Abandoned attempts are never charged against the retry budget: after a release (attempt Abandoned) and a
    // re-claim, the first *errored* attempt with maxAttempts = 2 still takes the retry branch -- if the abandoned
    // attempt were counted the budget would already be exhausted and the job would fail.
    [DatabaseTheory, DatabaseData]
    public async Task MarkAttemptErroredAsync_AbandonedAttemptsNotCounted(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var firstClaimTime = now.Add(-_releaseDelay).AddMinutes(-5);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: firstClaimTime);

        // First claim goes stale and is released -> its attempt is Abandoned.
        var firstClaim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, firstClaimTime, _releaseDelay);
        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);
        Assert.Contains(released, r => r.JobId == fixture.Job.Id);
        var abandoned = await pamRotationJobRepository.GetAttemptByIdAsync(firstClaim.AttemptId!.Value);
        Assert.Equal(PamRotationAttemptStatus.Abandoned, abandoned!.Status);

        // Second claim errors with maxAttempts = 2: errored count is 1 (the abandoned attempt is not charged), so
        // the retry branch is taken instead of failing the job.
        var secondClaim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, now, _releaseDelay);
        Assert.Equal(PamRotationClaimOutcome.Claimed, secondClaim.Outcome);
        var result = await pamRotationJobRepository.MarkAttemptErroredAsync(
            secondClaim.AttemptId!.Value, fixture.Daemon.Id, "flaky target", PamRotationSyncState.TargetUnchanged,
            now, maxAttempts: 2, TimeSpan.FromSeconds(60));

        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, result.Outcome);
        Assert.Equal(PamRotationJobStatus.Pending, result.JobStatus);
        Assert.Equal(1, result.ErroredAttemptCount);
    }

    [DatabaseTheory, DatabaseData]
    public async Task TimeoutDueAsync_PendingJobPastExpiry_TimedOutAsUnroutable(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: now.AddHours(-2), expiresAt: now.AddMinutes(-1));

        var timedOut = await pamRotationJobRepository.TimeoutDueAsync(now);

        // The sweep is set-based across the whole table, so scope the assertion to this test's job.
        var row = Assert.Single(timedOut, r => r.JobId == fixture.Job.Id);
        Assert.Equal(fixture.Config.Id, row.RotationConfigId);
        Assert.Equal(fixture.Organization.Id, row.OrganizationId);
        Assert.Equal(fixture.Cipher.Id, row.CipherId);
        Assert.Equal(PamRotationSource.Scheduled, row.Source);
        // Never claimed: unroutable, not stuck.
        Assert.Null(row.ClaimedByDaemonId);
        Assert.Equal(0, row.AttemptCount);

        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.TimedOut, job!.Status);
    }

    [DatabaseTheory, DatabaseData]
    public async Task TimeoutDueAsync_ClaimedJobPastExpiry_TimedOutAndAttemptAbandoned(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.AddHours(-2);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime, expiresAt: now.AddMinutes(-1));
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, claimTime, _releaseDelay);
        Assert.Equal(PamRotationClaimOutcome.Claimed, claim.Outcome);

        var timedOut = await pamRotationJobRepository.TimeoutDueAsync(now);

        var row = Assert.Single(timedOut, r => r.JobId == fixture.Job.Id);
        // Claimed at timeout: stuck, not unroutable.
        Assert.Equal(fixture.Daemon.Id, row.ClaimedByDaemonId);
        Assert.Equal(1, row.AttemptCount);

        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.TimedOut, job!.Status);
        Assert.Null(job.ClaimedByDaemonId);
        Assert.Null(job.ClaimedAt);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId!.Value);
        Assert.Equal(PamRotationAttemptStatus.Abandoned, attempt!.Status);
        Assert.Equal(now, attempt.ResolvedDate);
    }

    // Success wins: a job whose attempt reached Rotated is never timed out, no matter how far past ExpiresAt it is.
    // (A Rotated attempt only ever exists on a job MarkAttemptRotatedAsync atomically moved to Succeeded, so the
    // observable surface is a Succeeded job the sweep must leave alone; the sproc's NOT-EXISTS-Rotated guard is
    // defense in depth for the same rule.)
    [DatabaseTheory, DatabaseData]
    public async Task TimeoutDueAsync_JobWithRotatedAttempt_NotTimedOut(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.AddHours(-2);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime, expiresAt: now.AddMinutes(-1));
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, claimTime, _releaseDelay);
        Assert.Equal(PamRotationCipherWriteOutcome.Accepted, await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.Daemon.Id, "{\"rotated\":true}", fixture.Cipher.RevisionDate, claimTime));
        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, await pamRotationJobRepository.MarkAttemptRotatedAsync(
            claim.AttemptId.Value, fixture.Daemon.Id, PamSessionTerminationOutcome.NotRequested, claimTime));

        var timedOut = await pamRotationJobRepository.TimeoutDueAsync(now);

        Assert.DoesNotContain(timedOut, r => r.JobId == fixture.Job.Id);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Succeeded, job!.Status);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.Equal(PamRotationAttemptStatus.Rotated, attempt!.Status);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReleaseExpiredLeasesAsync_StaleDaemonPastExecuteBy_ReleasesJobAndAbandonsAttempt(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.Add(-_releaseDelay).AddMinutes(-5); // ExecuteBy = claimTime + releaseDelay < now.
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, claimTime, _releaseDelay);
        Assert.Equal(PamRotationClaimOutcome.Claimed, claim.Outcome);
        // The daemon never heartbeats, so it is stale (LastHeartbeatAt null).

        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);

        var row = Assert.Single(released, r => r.JobId == fixture.Job.Id);
        Assert.Equal(fixture.Config.Id, row.RotationConfigId);
        Assert.Equal(fixture.Organization.Id, row.OrganizationId);
        Assert.Equal(fixture.Cipher.Id, row.CipherId);
        // The pre-clear claimant survives on the audit row even though the job's own field is nulled.
        Assert.Equal(fixture.Daemon.Id, row.ClaimedByDaemonId);

        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Pending, job!.Status);
        Assert.Null(job.ClaimedByDaemonId);
        Assert.Null(job.ClaimedAt);
        // Re-claimable exactly at the lease's end (pre-clear ClaimedAt + releaseDelay), not at the sweep's run time.
        Assert.Equal(claimTime.Add(_releaseDelay), job.NextClaimableAt);

        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId!.Value);
        Assert.Equal(PamRotationAttemptStatus.Abandoned, attempt!.Status);
        Assert.Equal(now, attempt.ResolvedDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReleaseExpiredLeasesAsync_FreshHeartbeat_NotReleased(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.Add(-_releaseDelay).AddMinutes(-5); // Lease expired...
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, claimTime, _releaseDelay);
        // ...but the daemon is slow, not gone: a fresh heartbeat keeps the claim alive (success wins for a
        // slow-but-live daemon whose report may still land).
        await pamDaemonRepository.UpdateHeartbeatAsync(fixture.Daemon.Id, now, TimeSpan.FromSeconds(15));

        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);

        Assert.DoesNotContain(released, r => r.JobId == fixture.Job.Id);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Claimed, job!.Status);
        Assert.Equal(fixture.Daemon.Id, job.ClaimedByDaemonId);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId!.Value);
        Assert.Equal(PamRotationAttemptStatus.Executing, attempt!.Status);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReleaseExpiredLeasesAsync_WithinLease_NotReleased(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository, now: now);
        // Claimed just now: the daemon is heartbeat-stale (it never beat), but ExecuteBy is still releaseDelay away --
        // release fires at lease expiry, never at stale detection.
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, now, _releaseDelay);

        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);

        Assert.DoesNotContain(released, r => r.JobId == fixture.Job.Id);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Claimed, job!.Status);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId!.Value);
        Assert.Equal(PamRotationAttemptStatus.Executing, attempt!.Status);
    }

    // Success wins on the release path too: a job whose attempt reached Rotated is never released. As with the
    // timeout sweep, the reachable surface is the Succeeded job the atomic success report produced.
    [DatabaseTheory, DatabaseData]
    public async Task ReleaseExpiredLeasesAsync_JobWithRotatedAttempt_NotReleased(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
        ICipherRepository cipherRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IPamRotationJobRepository pamRotationJobRepository)
    {
        var now = DateTime.UtcNow;
        var claimTime = now.Add(-_releaseDelay).AddMinutes(-5);
        var fixture = await SeedClaimableJobAsync(organizationRepository, pamTargetSystemRepository, apiKeyRepository,
            pamDaemonRepository, cipherRepository, pamRotationConfigRepository, pamRotationJobRepository,
            now: claimTime);
        var claim = await pamRotationJobRepository.ClaimAsync(
            fixture.Job.Id, fixture.Daemon.Id, claimTime, _releaseDelay);
        Assert.Equal(PamRotationCipherWriteOutcome.Accepted, await pamRotationJobRepository.AcceptCipherWriteAsync(
            claim.AttemptId!.Value, fixture.Daemon.Id, "{\"rotated\":true}", fixture.Cipher.RevisionDate, claimTime));
        Assert.Equal(PamRotationAttemptResolveOutcome.Resolved, await pamRotationJobRepository.MarkAttemptRotatedAsync(
            claim.AttemptId.Value, fixture.Daemon.Id, PamSessionTerminationOutcome.NotRequested, claimTime));

        var released = await pamRotationJobRepository.ReleaseExpiredLeasesAsync(now, _offlineAfter, _releaseDelay);

        Assert.DoesNotContain(released, r => r.JobId == fixture.Job.Id);
        var job = await pamRotationJobRepository.GetByIdAsync(fixture.Job.Id);
        Assert.Equal(PamRotationJobStatus.Succeeded, job!.Status);
        var attempt = await pamRotationJobRepository.GetAttemptByIdAsync(claim.AttemptId.Value);
        Assert.Equal(PamRotationAttemptStatus.Rotated, attempt!.Status);
    }

    private sealed record ClaimableJobFixture(
        Organization Organization,
        PamTargetSystem Target,
        PamDaemon Daemon,
        Cipher Cipher,
        PamRotationConfig Config,
        PamRotationJob Job,
        DateTime Now);

    /// <summary>
    /// Seeds the full eligibility graph for a claimable job: org, active automatic target, enrolled+assigned daemon,
    /// org cipher, enabled config, and a Pending job created through the guarded sproc. <paramref name="now"/> lets
    /// sweep tests place the whole graph in the past; <paramref name="expiresAt"/>/<paramref name="nextClaimableAt"/>
    /// override the job's window (defaults keep it claimable now and far from any concurrently-running sweep test's
    /// cutoff).
    /// </summary>
    private static async Task<ClaimableJobFixture> SeedClaimableJobAsync(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IApiKeyRepository apiKeyRepository,
        IPamDaemonRepository pamDaemonRepository,
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
        var daemon = await CreateEnrolledDaemonAsync(apiKeyRepository, pamDaemonRepository, organization.Id);
        await AssignAsync(pamDaemonRepository, daemon.Id, target.Id, organization.Id, seedNow);
        var cipher = await CreateCipherAsync(cipherRepository, organization.Id);
        var config = await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, cipher.Id, target.Id, seedNow));

        var job = BuildPendingJob(config.Id, seedNow, expiresAt, nextClaimableAt);
        Assert.Equal(PamRotationJobCreateOutcome.Created, await pamRotationJobRepository.CreateGuardedAsync(job));

        return new ClaimableJobFixture(organization, target, daemon, cipher, config, job, seedNow);
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
            Status = PamDaemonStatus.Enrolled,
        });
    }

    private static async Task AssignAsync(
        IPamDaemonRepository pamDaemonRepository, Guid daemonId, Guid targetSystemId, Guid organizationId, DateTime now)
        => await pamDaemonRepository.CreateAssignmentAsync(new PamDaemonTargetAssignment
        {
            Id = CoreHelpers.GenerateComb(),
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

    // ExpiresAt defaults far into the future so a concurrently-running timeout-sweep test (the sweeps are set-based
    // across the whole table) never times this job out from under its own test.
    private static PamRotationJob BuildPendingJob(
        Guid configId, DateTime now, DateTime? expiresAt = null, DateTime? nextClaimableAt = null) => new()
        {
            Id = CoreHelpers.GenerateComb(),
            RotationConfigId = configId,
            Source = PamRotationSource.Scheduled,
            Status = PamRotationJobStatus.Pending,
            CreationDate = now,
            NextClaimableAt = nextClaimableAt ?? now,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(1),
        };
}

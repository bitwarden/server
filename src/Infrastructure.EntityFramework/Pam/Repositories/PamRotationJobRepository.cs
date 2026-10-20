using System.Data;
using AutoMapper;
using Bit.Core.Utilities;
using Bit.Core.Vault.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EfAttempt = Bit.Infrastructure.EntityFramework.Pam.Models.PamRotationAttempt;
using EfJob = Bit.Infrastructure.EntityFramework.Pam.Models.PamRotationJob;
using EfTimeoutSweep = Bit.Infrastructure.EntityFramework.Pam.Models.PamRotationJobTimeoutSweep;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Repositories;

/// <summary>
/// Rebuilds the procedures' <c>UPDLOCK, HOLDLOCK</c> range locks and <c>OUTPUT</c> clauses from serializable
/// transactions and guarded <c>ExecuteUpdate</c> calls, enforcing the same invariants.
/// </summary>
public class PamRotationJobRepository : BaseEntityFrameworkRepository, IPamRotationJobRepository
{
    public PamRotationJobRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    { }

    public async Task<PamRotationJobCreateOutcome> CreateGuardedAsync(PamRotationJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return await SerializableRetry.RunAsync(() => WriteGuardedJobAsync(job));
    }

    private async Task<PamRotationJobCreateOutcome> WriteGuardedJobAsync(PamRotationJob job)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        // A loser aborted at commit by a concurrent Serializable transaction is replayed by SerializableRetry.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // Re-checked so a config or target disabled between read and write cannot mint a job.
        var offerable = await dbContext.PamRotationConfigs
            .Join(dbContext.PamTargetSystems, c => c.TargetSystemId, t => t.Id, (c, t) => new { Config = c, Target = t })
            .AnyAsync(x => x.Config.Id == job.RotationConfigId
                && x.Config.Enabled
                && x.Target.Method == PamTargetSystemMethod.Automatic
                && x.Target.Status == PamTargetSystemStatus.Active);
        if (!offerable)
        {
            await transaction.RollbackAsync();
            return PamRotationJobCreateOutcome.ConfigNotOfferable;
        }

        // AtMostOneActiveJobPerConfig: Serializable holds the predicate's range, so a concurrent create for the same
        // config fails to serialize. A timed-out job holds its config until the sweep records it, so the sweep's
        // reschedule lands first.
        var hasActiveJob = await dbContext.PamRotationJobs
            .AnyAsync(j => j.RotationConfigId == job.RotationConfigId
                && (j.Action == PamRotationJobAction.None || j.Action == PamRotationJobAction.Claimed)
                && !dbContext.PamRotationJobTimeoutSweeps.Any(s => s.RotationJobId == j.Id));
        if (hasActiveJob)
        {
            await transaction.RollbackAsync();
            return PamRotationJobCreateOutcome.ActiveJobExists;
        }

        await dbContext.PamRotationJobs.AddAsync(Mapper.Map<EfJob>(job));

        try
        {
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // A serialization failure on the concurrent path reads the same as losing the guard above.
            await transaction.RollbackAsync();
            return PamRotationJobCreateOutcome.ActiveJobExists;
        }

        return PamRotationJobCreateOutcome.Created;
    }

    public async Task<PamRotationJob?> GetByIdAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var job = await dbContext.PamRotationJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);
        return Mapper.Map<PamRotationJob>(job);
    }

    public async Task<PamRotationClaimResult> ClaimAsync(
        Guid jobId,
        Guid accessConnectorId,
        DateTime now,
        TimeSpan releaseDelay)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // First-claim-wins: the None predicate is evaluated under the row lock this UPDATE itself takes, so concurrent
        // claims serialize.
        var claimed = await EligibleJobs(dbContext, accessConnectorId)
            .Where(x => x.Job.Id == jobId
                && x.Job.Action == PamRotationJobAction.None
                && x.Job.ExpirationDate > now
                && x.Job.NextClaimableDate <= now
                && x.Config.Enabled
                && x.Target.Status == PamTargetSystemStatus.Active)
            .Select(x => x.Job)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Action, PamRotationJobAction.Claimed)
                .SetProperty(j => j.ClaimedByAccessConnectorId, accessConnectorId)
                .SetProperty(j => j.ClaimedDate, now));

        if (claimed == 0)
        {
            // An unknown job and one this access connector may not claim both yield NotEligible, which the caller maps
            // to 404, so the outcome reveals nothing about existence.
            var eligible = await EligibleJobs(dbContext, accessConnectorId).AnyAsync(x => x.Job.Id == jobId);
            await transaction.RollbackAsync();

            return new PamRotationClaimResult
            {
                Outcome = eligible ? PamRotationClaimOutcome.NotClaimable : PamRotationClaimOutcome.NotEligible,
            };
        }

        // AtMostOneInFlightAttemptPerJob: inserted in the claim's own transaction, so a claimed job always has exactly
        // one in-flight attempt. Its CreationDate equals the job's ClaimedDate, which is how the claim's attempt is
        // recognised later.
        var attempt = new EfAttempt
        {
            Id = CombGuid.Generate(),
            JobId = jobId,
            ClaimedByAccessConnectorId = accessConnectorId,
            CipherUpdated = false,
            Action = PamRotationAttemptAction.None,
            CreationDate = now,
        };
        await dbContext.PamRotationAttempts.AddAsync(attempt);
        await dbContext.SaveChangesAsync();

        var snapshot = await dbContext.PamRotationJobs
            .Where(j => j.Id == jobId)
            .Join(dbContext.PamRotationConfigs, j => j.RotationConfigId, c => c.Id, (j, c) => new { Job = j, Config = c })
            .Join(dbContext.PamTargetSystems, x => x.Config.TargetSystemId, t => t.Id, (x, t) => new
            {
                x.Job.Source,
                TargetSystemId = t.Id,
                TargetSystemName = t.Name,
                t.SealedData,
                TargetSystemWrappedKey = t.WrappedKey,
                x.Config.CipherId,
                RotationConfigSealedData = x.Config.SealedData,
                RotationConfigWrappedKey = x.Config.WrappedKey,
            })
            .AsNoTracking()
            .FirstAsync();

        await transaction.CommitAsync();

        return new PamRotationClaimResult
        {
            Outcome = PamRotationClaimOutcome.Claimed,
            AttemptId = attempt.Id,
            JobId = jobId,
            Source = snapshot.Source,
            TargetSystemId = snapshot.TargetSystemId,
            TargetSystemName = snapshot.TargetSystemName,
            TargetSystemSealedData = snapshot.SealedData,
            TargetSystemWrappedKey = snapshot.TargetSystemWrappedKey,
            CipherId = snapshot.CipherId,
            RotationConfigSealedData = snapshot.RotationConfigSealedData,
            RotationConfigWrappedKey = snapshot.RotationConfigWrappedKey,
            ExecuteBy = now + releaseDelay,
        };
    }

    public async Task<ICollection<PamClaimableJob>> GetManyClaimableByAccessConnectorIdAsync(
        Guid accessConnectorId,
        DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Re-derives every condition ClaimAsync re-checks, so the list an access connector sees and what it can claim
        // agree.
        return await EligibleJobs(dbContext, accessConnectorId)
            .Where(x => x.Job.Action == PamRotationJobAction.None
                && x.Job.ExpirationDate > now
                && x.Job.NextClaimableDate <= now
                && x.Config.Enabled
                && x.Target.Status == PamTargetSystemStatus.Active)
            .Select(x => new PamClaimableJob
            {
                Id = x.Job.Id,
                RotationConfigId = x.Job.RotationConfigId,
                Source = x.Job.Source,
                Action = x.Job.Action,
                ClaimedByAccessConnectorId = x.Job.ClaimedByAccessConnectorId,
                ClaimedDate = x.Job.ClaimedDate,
                CreationDate = x.Job.CreationDate,
                NextClaimableDate = x.Job.NextClaimableDate,
                ExpirationDate = x.Job.ExpirationDate,
                TargetSystemId = x.Config.TargetSystemId,
            })
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ICollection<PamRotationJobDetails>> GetManyByConfigIdAsync(Guid configId, int limit, DateTime since,
        DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var jobs = await dbContext.PamRotationJobs
            .Where(j => j.RotationConfigId == configId && j.CreationDate >= since)
            .OrderByDescending(j => j.CreationDate)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();
        if (jobs.Count == 0)
        {
            return new List<PamRotationJobDetails>();
        }

        var jobIds = jobs.Select(j => j.Id).ToList();
        var attempts = await dbContext.PamRotationAttempts
            .Where(a => jobIds.Contains(a.JobId))
            .OrderBy(a => a.CreationDate)
            .AsNoTracking()
            .ToListAsync();

        var attemptsByJob = attempts
            .GroupBy(a => a.JobId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PamRotationAttempt>)Mapper.Map<List<PamRotationAttempt>>(g.ToList()));

        return jobs
            .Select(job => PamRotationJobDetails.From(
                Mapper.Map<PamRotationJob>(job),
                attemptsByJob.TryGetValue(job.Id, out var jobAttempts) ? jobAttempts : [],
                now))
            .ToList();
    }

    public async Task<ICollection<PamRotationJobDetails>> GetManyRecentByAccessConnectorIdAsync(
        Guid accessConnectorId,
        int limit,
        DateTime since,
        DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var jobs = await dbContext.PamRotationJobs
            .Where(j => dbContext.PamRotationAttempts
                .Any(a => a.JobId == j.Id && a.ClaimedByAccessConnectorId == accessConnectorId
                    && a.CreationDate >= since))
            .OrderByDescending(j => j.CreationDate)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();
        if (jobs.Count == 0)
        {
            return new List<PamRotationJobDetails>();
        }

        var jobIds = jobs.Select(j => j.Id).ToList();
        var attempts = await dbContext.PamRotationAttempts
            .Where(a => a.ClaimedByAccessConnectorId == accessConnectorId && jobIds.Contains(a.JobId))
            .OrderBy(a => a.CreationDate)
            .AsNoTracking()
            .ToListAsync();

        var attemptsByJob = attempts
            .GroupBy(a => a.JobId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PamRotationAttempt>)Mapper.Map<List<PamRotationAttempt>>(g.ToList()));

        return jobs
            .Select(job => PamRotationJobDetails.From(
                Mapper.Map<PamRotationJob>(job),
                attemptsByJob.TryGetValue(job.Id, out var jobAttempts) ? jobAttempts : [],
                now))
            .ToList();
    }

    public async Task<PamRotationAttempt?> GetAttemptByIdAsync(Guid attemptId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var attempt = await dbContext.PamRotationAttempts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attemptId);
        return Mapper.Map<PamRotationAttempt>(attempt);
    }

    public async Task<PamRotationCipherWriteOutcome> AcceptCipherWriteAsync(Guid attemptId, Guid accessConnectorId,
        string cipherData, DateTime lastKnownRevisionDate, DateTime now)
        => await SerializableRetry.RunAsync(() =>
            AcceptCipherWriteInternalAsync(attemptId, accessConnectorId, cipherData, lastKnownRevisionDate, now));

    private async Task<PamRotationCipherWriteOutcome> AcceptCipherWriteInternalAsync(Guid attemptId,
        Guid accessConnectorId, string cipherData, DateTime lastKnownRevisionDate, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        // Not Serializable: there the account bump's reads take shared locks on User rows, so two rotations in one org
        // deadlock on MySQL and a user's save aborts the rotation on PostgreSQL. ReadCommitted is safe because the claim
        // is re-checked under the job row's lock below and the cipher write is conditional.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        var target = await dbContext.PamRotationAttempts
            .Where(a => a.Id == attemptId
                && a.Action == PamRotationAttemptAction.None
                && a.ClaimedByAccessConnectorId == accessConnectorId)
            .Join(dbContext.PamRotationJobs, a => a.JobId, j => j.Id, (a, j) => new { Attempt = a, Job = j })
            .Where(x => x.Job.Action == PamRotationJobAction.Claimed
                && x.Job.ExpirationDate > now
                && x.Job.ClaimedByAccessConnectorId == accessConnectorId
                && x.Job.ClaimedDate == x.Attempt.CreationDate)
            .Join(dbContext.PamRotationConfigs, x => x.Job.RotationConfigId, c => c.Id, (x, c) => new
            {
                JobId = x.Job.Id,
                AttemptCreationDate = x.Attempt.CreationDate,
                c.CipherId,
                c.OrganizationId,
            })
            .FirstOrDefaultAsync();

        if (target is null)
        {
            await transaction.RollbackAsync();
            return PamRotationCipherWriteOutcome.Rejected;
        }

        // Locks the job row and re-checks the claim, the counterpart of the procedure's UPDLOCK. A concurrent release
        // either committed first (0 rows) or waits for this transaction.
        var claimed = await dbContext.PamRotationJobs
            .Where(j => j.Id == target.JobId
                && j.Action == PamRotationJobAction.Claimed
                && j.ExpirationDate > now
                && j.ClaimedByAccessConnectorId == accessConnectorId
                && j.ClaimedDate == target.AttemptCreationDate)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.ClaimedDate, j => j.ClaimedDate));
        if (claimed == 0)
        {
            await transaction.RollbackAsync();
            return PamRotationCipherWriteOutcome.Rejected;
        }

        // Vault's write joins this transaction. A deleted cipher (the config has no FK to Cipher) is Rejected, and a
        // concurrent user edit is refused rather than clobbered.
        var cipherResult = await dbContext.CipherUpdateDataAsync(
            target.CipherId, target.OrganizationId, cipherData, lastKnownRevisionDate, now);
        if (cipherResult != CipherDataUpdateResult.Updated)
        {
            await transaction.RollbackAsync();
            return cipherResult == CipherDataUpdateResult.RevisionMismatch
                ? PamRotationCipherWriteOutcome.RevisionMismatch
                : PamRotationCipherWriteOutcome.Rejected;
        }

        var attempt = await dbContext.PamRotationAttempts.FirstAsync(a => a.Id == attemptId);
        attempt.CipherUpdated = true;

        // One save persists the attempt flag and the account revision bumps.
        await dbContext.SaveChangesAsync();

        await transaction.CommitAsync();
        return PamRotationCipherWriteOutcome.Accepted;
    }

    public async Task<PamRotationAttemptResolveOutcome> MarkAttemptRotatedAsync(Guid attemptId, Guid accessConnectorId,
        PamSessionTerminationOutcome sessionTermination, DateTime now)
        => await SerializableRetry.RunAsync(() =>
            MarkAttemptRotatedInternalAsync(attemptId, accessConnectorId, sessionTermination, now));

    private async Task<PamRotationAttemptResolveOutcome> MarkAttemptRotatedInternalAsync(Guid attemptId,
        Guid accessConnectorId, PamSessionTerminationOutcome sessionTermination, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // CipherUpdated is the VerifiedBeforeSuccess backstop: an access connector cannot report success for a rotation
        // whose new secret never reached the vault.
        var jobId = await dbContext.PamRotationAttempts
            .Where(a => a.Id == attemptId
                && a.Action == PamRotationAttemptAction.None
                && a.ClaimedByAccessConnectorId == accessConnectorId
                && a.CipherUpdated)
            .Join(dbContext.PamRotationJobs, a => a.JobId, j => j.Id, (a, j) => new { Attempt = a, Job = j })
            .Where(x => x.Job.Action == PamRotationJobAction.Claimed
                && x.Job.ExpirationDate > now
                && x.Job.ClaimedByAccessConnectorId == accessConnectorId
                && x.Job.ClaimedDate == x.Attempt.CreationDate)
            .Select(x => (Guid?)x.Job.Id)
            .FirstOrDefaultAsync();

        if (jobId is null)
        {
            await transaction.RollbackAsync();
            return PamRotationAttemptResolveOutcome.Rejected;
        }

        await dbContext.PamRotationAttempts
            .Where(a => a.Id == attemptId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Action, PamRotationAttemptAction.Rotated)
                .SetProperty(a => a.SessionTermination, sessionTermination)
                .SetProperty(a => a.ResolvedDate, now));

        // Written with the attempt, so a Rotated attempt always has a Succeeded job: success wins by construction.
        await dbContext.PamRotationJobs
            .Where(j => j.Id == jobId.Value)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Action, PamRotationJobAction.Succeeded)
                .SetProperty(j => j.ClaimedByAccessConnectorId, (Guid?)null)
                .SetProperty(j => j.ClaimedDate, (DateTime?)null));

        await transaction.CommitAsync();
        return PamRotationAttemptResolveOutcome.Resolved;
    }

    public async Task<PamRotationFailureResult> MarkAttemptErroredAsync(Guid attemptId, Guid accessConnectorId,
        string? failureReason, PamRotationSyncState syncState, DateTime now, int maxAttempts, TimeSpan retryBaseDelay)
        => await SerializableRetry.RunAsync(() => MarkAttemptErroredInternalAsync(attemptId, accessConnectorId,
            failureReason, syncState, now, maxAttempts, retryBaseDelay));

    private async Task<PamRotationFailureResult> MarkAttemptErroredInternalAsync(Guid attemptId,
        Guid accessConnectorId, string? failureReason, PamRotationSyncState syncState, DateTime now, int maxAttempts,
        TimeSpan retryBaseDelay)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var jobId = await dbContext.PamRotationAttempts
            .Where(a => a.Id == attemptId
                && a.Action == PamRotationAttemptAction.None
                && a.ClaimedByAccessConnectorId == accessConnectorId)
            .Join(dbContext.PamRotationJobs, a => a.JobId, j => j.Id, (a, j) => new { Attempt = a, Job = j })
            .Where(x => x.Job.Action == PamRotationJobAction.Claimed
                && x.Job.ExpirationDate > now
                && x.Job.ClaimedByAccessConnectorId == accessConnectorId
                && x.Job.ClaimedDate == x.Attempt.CreationDate)
            .Select(x => (Guid?)x.Job.Id)
            .FirstOrDefaultAsync();

        if (jobId is null)
        {
            await transaction.RollbackAsync();
            return new PamRotationFailureResult { Outcome = PamRotationAttemptResolveOutcome.Rejected };
        }

        await dbContext.PamRotationAttempts
            .Where(a => a.Id == attemptId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Action, PamRotationAttemptAction.Errored)
                .SetProperty(a => a.FailureReason, failureReason)
                .SetProperty(a => a.SyncState, syncState)
                .SetProperty(a => a.ResolvedDate, now));

        // Abandoned attempts are not counted, so a release or timeout does not charge the retry budget.
        var erroredCount = await dbContext.PamRotationAttempts
            .CountAsync(a => a.JobId == jobId.Value && a.Action == PamRotationAttemptAction.Errored);

        // The status the job derives as right after this write; the guard saw it unexpired.
        PamRotationJobStatus jobStatus;
        if (erroredCount < maxAttempts)
        {
            jobStatus = PamRotationJobStatus.Pending;
            var backoff = TimeSpan.FromSeconds(retryBaseDelay.TotalSeconds * Math.Pow(2, erroredCount - 1));
            var nextClaimableDate = now + backoff;
            await dbContext.PamRotationJobs
                .Where(j => j.Id == jobId.Value)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Action, PamRotationJobAction.None)
                    .SetProperty(j => j.ClaimedByAccessConnectorId, (Guid?)null)
                    .SetProperty(j => j.ClaimedDate, (DateTime?)null)
                    .SetProperty(j => j.NextClaimableDate, nextClaimableDate));
        }
        else
        {
            jobStatus = PamRotationJobStatus.Failed;
            await dbContext.PamRotationJobs
                .Where(j => j.Id == jobId.Value)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Action, PamRotationJobAction.Failed)
                    .SetProperty(j => j.ClaimedByAccessConnectorId, (Guid?)null)
                    .SetProperty(j => j.ClaimedDate, (DateTime?)null));
        }

        await transaction.CommitAsync();

        return new PamRotationFailureResult
        {
            Outcome = PamRotationAttemptResolveOutcome.Resolved,
            JobStatus = jobStatus,
            ErroredAttemptCount = erroredCount,
        };
    }

    public async Task<IReadOnlyList<PamTimedOutJob>> TimeoutDueAsync(DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // A timeout is derived; the PamRotationJobTimeoutSweep primary key fails a losing sweep's SaveChanges, so no
        // stronger isolation is needed. A Rotated attempt always has a Succeeded job, so success wins without checking
        // attempts.
        var due = await dbContext.PamRotationJobs
            .Where(j => (j.Action == PamRotationJobAction.None || j.Action == PamRotationJobAction.Claimed)
                && j.ExpirationDate <= now
                && !dbContext.PamRotationJobTimeoutSweeps.Any(s => s.RotationJobId == j.Id))
            .Join(dbContext.PamRotationConfigs, j => j.RotationConfigId, c => c.Id, (j, c) => new
            {
                JobId = j.Id,
                RotationConfigId = c.Id,
                c.OrganizationId,
                c.CipherId,
                j.Source,
                j.ClaimedByAccessConnectorId,
            })
            .AsNoTracking()
            .ToListAsync();

        if (due.Count == 0)
        {
            return [];
        }

        var jobIds = due.Select(d => d.JobId).ToList();
        dbContext.PamRotationJobTimeoutSweeps.AddRange(jobIds.Select(id =>
            new EfTimeoutSweep { RotationJobId = id, SweptDate = now }));
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent sweep journaled some of these first. Report nothing; the next run picks up the rest.
            if (await dbContext.PamRotationJobTimeoutSweeps.AnyAsync(s => jobIds.Contains(s.RotationJobId)))
            {
                return [];
            }

            throw;
        }

        var attemptCounts = await AttemptCountsAsync(dbContext, jobIds);

        return due
            .Select(d => new PamTimedOutJob
            {
                JobId = d.JobId,
                RotationConfigId = d.RotationConfigId,
                OrganizationId = d.OrganizationId,
                CipherId = d.CipherId,
                Source = d.Source,
                ClaimedByAccessConnectorId = d.ClaimedByAccessConnectorId,
                AttemptCount = attemptCounts.TryGetValue(d.JobId, out var count) ? count : 0,
            })
            .ToList();
    }

    public async Task<IReadOnlyList<PamReleasedJob>> ReleaseExpiredLeasesAsync(DateTime now, TimeSpan offlineAfter,
        TimeSpan releaseDelay)
        => await SerializableRetry.RunAsync(() => ReleaseExpiredLeasesInternalAsync(now, offlineAfter, releaseDelay));

    private async Task<IReadOnlyList<PamReleasedJob>> ReleaseExpiredLeasesInternalAsync(DateTime now,
        TimeSpan offlineAfter, TimeSpan releaseDelay)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var staleBefore = now - offlineAfter;

        // Needs an expired lease, not only a stale heartbeat, to preserve success-wins. A disabled access connector's
        // jobs still release. A timed-out claim is the timeout sweep's; a Rotated attempt always has a Succeeded job.
        var candidates = await dbContext.PamRotationJobs
            .Where(j => j.Action == PamRotationJobAction.Claimed && j.ExpirationDate > now && j.ClaimedDate != null)
            .Join(dbContext.PamAccessConnectors, j => j.ClaimedByAccessConnectorId, d => d.Id,
                (j, d) => new { Job = j, AccessConnector = d })
            .Where(x => x.AccessConnector.LastHeartbeatDate == null
                || x.AccessConnector.LastHeartbeatDate < staleBefore)
            .Join(dbContext.PamRotationConfigs, x => x.Job.RotationConfigId, c => c.Id, (x, c) => new
            {
                JobId = x.Job.Id,
                x.Job.ClaimedDate,
                RotationConfigId = c.Id,
                c.OrganizationId,
                c.CipherId,
                x.Job.Source,
                PreviousClaimedByAccessConnectorId = x.Job.ClaimedByAccessConnectorId,
            })
            .AsNoTracking()
            .ToListAsync();

        // Computed in memory from the pre-clear ClaimedDate, since MySQL's UPDATE assigns left to right and would
        // otherwise read the nulled column.
        var released = candidates
            .Where(c => c.ClaimedDate!.Value + releaseDelay <= now)
            .ToList();
        if (released.Count == 0)
        {
            await transaction.CommitAsync();
            return [];
        }

        // Only jobs whose update landed count as released, as with the procedure's OUTPUT.
        var releasedJobIds = new List<Guid>();
        foreach (var job in released)
        {
            var nextClaimableDate = job.ClaimedDate!.Value + releaseDelay;
            var updated = await dbContext.PamRotationJobs
                .Where(j => j.Id == job.JobId && j.Action == PamRotationJobAction.Claimed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Action, PamRotationJobAction.None)
                    .SetProperty(j => j.NextClaimableDate, nextClaimableDate)
                    .SetProperty(j => j.ClaimedByAccessConnectorId, (Guid?)null)
                    .SetProperty(j => j.ClaimedDate, (DateTime?)null));
            if (updated == 1)
            {
                releasedJobIds.Add(job.JobId);
            }
        }

        // Records when the released claim's attempt ended; it derives as Abandoned, which the retry budget never
        // charges.
        await RecordReleasedAttemptsAsync(dbContext, releasedJobIds, now);

        await transaction.CommitAsync();

        return released
            .Where(r => releasedJobIds.Contains(r.JobId))
            .Select(r => new PamReleasedJob
            {
                JobId = r.JobId,
                RotationConfigId = r.RotationConfigId,
                OrganizationId = r.OrganizationId,
                CipherId = r.CipherId,
                Source = r.Source,
                ClaimedByAccessConnectorId = r.PreviousClaimedByAccessConnectorId!.Value,
            })
            .ToList();
    }

    /// <remarks>
    /// The join every eligibility decision shares. It re-checks that the access connector is enabled and in the
    /// config's organization.
    /// </remarks>
    private static IQueryable<EligibleJob> EligibleJobs(DatabaseContext dbContext, Guid accessConnectorId) =>
        dbContext.PamRotationJobs
            .Join(dbContext.PamRotationConfigs, j => j.RotationConfigId, c => c.Id, (j, c) => new { Job = j, Config = c })
            .Join(dbContext.PamTargetSystems, x => x.Config.TargetSystemId, t => t.Id, (x, t) => new { x.Job, x.Config, Target = t })
            .Where(x => dbContext.PamAccessConnectorTargetAssignments.Any(a =>
                a.AccessConnectorId == accessConnectorId && a.TargetSystemId == x.Config.TargetSystemId))
            .Join(dbContext.PamAccessConnectors.Where(
                d => d.Id == accessConnectorId && d.Status == PamAccessConnectorStatus.Enabled),
                x => x.Config.OrganizationId, d => d.OrganizationId,
                (x, d) => new EligibleJob { Job = x.Job, Config = x.Config, Target = x.Target });

    private static Task RecordReleasedAttemptsAsync(DatabaseContext dbContext, List<Guid> jobIds, DateTime now) =>
        dbContext.PamRotationAttempts
            .Where(a => jobIds.Contains(a.JobId) && a.Action == PamRotationAttemptAction.None && a.ResolvedDate == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.ResolvedDate, now));

    private static async Task<Dictionary<Guid, int>> AttemptCountsAsync(DatabaseContext dbContext, List<Guid> jobIds) =>
        await dbContext.PamRotationAttempts
            .Where(a => jobIds.Contains(a.JobId))
            .GroupBy(a => a.JobId)
            .Select(g => new { JobId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.JobId, x => x.Count);

    private sealed class EligibleJob
    {
        public required EfJob Job { get; init; }
        public required Models.PamRotationConfig Config { get; init; }
        public required Models.PamTargetSystem Target { get; init; }
    }
}

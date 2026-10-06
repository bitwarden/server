using System.Data;
using AutoMapper;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
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
/// EF counterpart of the MSSQL rotation-job procedures, rebuilding their <c>UPDLOCK, HOLDLOCK</c> range locks and
/// <c>OUTPUT</c> clause from two portable primitives: a serializable transaction, and a single <c>ExecuteUpdate</c>
/// whose <c>WHERE</c> carries the guard. Enforces the same invariants: <c>AtMostOneActiveJobPerConfig</c>,
/// <c>AtMostOneInFlightAttemptPerJob</c>, first-claim-wins, and <c>VerifiedBeforeSuccess</c>.
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

        // Re-checked here, not just by the caller, so a config disabled or target switched to Manual meanwhile can't mint a job.
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

        // AtMostOneActiveJobPerConfig: Serializable holds the predicate's range, so a concurrent create for the same config fails to serialize instead of duplicating.
        // A timed-out job holds its config until the timeout sweep records it, so the sweep's reschedule lands first.
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

        // First-claim-wins: the None predicate is evaluated under the row lock this UPDATE itself takes, so concurrent claims serialize.
        var claimed = await EligibleJobs(dbContext, accessConnectorId)
            .Where(x => x.Job.Id == jobId
                && x.Job.Action == PamRotationJobAction.None
                && x.Job.ExpiresAt > now
                && x.Job.NextClaimableAt <= now
                && x.Config.Enabled
                && x.Target.Status == PamTargetSystemStatus.Active)
            .Select(x => x.Job)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Action, PamRotationJobAction.Claimed)
                .SetProperty(j => j.ClaimedByAccessConnectorId, accessConnectorId)
                .SetProperty(j => j.ClaimedAt, now));

        if (claimed == 0)
        {
            // Classify eligibility first, so an unknown job and one this access connector may not claim produce the
            // same NotEligible outcome -- the caller maps it to 404, leaving no existence oracle.
            var eligible = await EligibleJobs(dbContext, accessConnectorId).AnyAsync(x => x.Job.Id == jobId);
            await transaction.RollbackAsync();

            return new PamRotationClaimResult
            {
                Outcome = eligible ? PamRotationClaimOutcome.NotClaimable : PamRotationClaimOutcome.NotEligible,
            };
        }

        // AtMostOneInFlightAttemptPerJob: the attempt is inserted in the claim's own transaction, so a claimed job
        // always has exactly one in-flight attempt from the moment it is claimed. Its CreationDate equals the job's
        // ClaimedAt, which is how the claim's own attempt is recognised later.
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
                t.Kind,
                t.PasswordPolicy,
                x.Config.CipherId,
                x.Config.AccountIdentity,
                x.Config.TerminateSessions,
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
            Kind = snapshot.Kind,
            PasswordPolicy = snapshot.PasswordPolicy,
            CipherId = snapshot.CipherId,
            AccountIdentity = snapshot.AccountIdentity,
            TerminateSessions = snapshot.TerminateSessions,
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
                && x.Job.ExpiresAt > now
                && x.Job.NextClaimableAt <= now
                && x.Config.Enabled
                && x.Target.Status == PamTargetSystemStatus.Active)
            .Select(x => new PamClaimableJob
            {
                Id = x.Job.Id,
                RotationConfigId = x.Job.RotationConfigId,
                Source = x.Job.Source,
                Action = x.Job.Action,
                ClaimedByAccessConnectorId = x.Job.ClaimedByAccessConnectorId,
                ClaimedAt = x.Job.ClaimedAt,
                CreationDate = x.Job.CreationDate,
                NextClaimableAt = x.Job.NextClaimableAt,
                ExpiresAt = x.Job.ExpiresAt,
                TargetSystemId = x.Config.TargetSystemId,
            })
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ICollection<PamRotationJobDetails>> GetManyByConfigIdAsync(Guid configId, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var jobs = await dbContext.PamRotationJobs
            .Where(j => j.RotationConfigId == configId)
            .OrderByDescending(j => j.CreationDate)
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
        DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var jobs = await dbContext.PamRotationJobs
            .Where(j => dbContext.PamRotationAttempts
                .Any(a => a.JobId == j.Id && a.ClaimedByAccessConnectorId == accessConnectorId))
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
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // Serializable stands in for the MSSQL UPDLOCK on the job row, closing the check-then-act window against a concurrent release/timeout sweep.
        var target = await dbContext.PamRotationAttempts
            .Where(a => a.Id == attemptId
                && a.Action == PamRotationAttemptAction.None
                && a.ClaimedByAccessConnectorId == accessConnectorId)
            .Join(dbContext.PamRotationJobs, a => a.JobId, j => j.Id, (a, j) => new { Attempt = a, Job = j })
            .Where(x => x.Job.Action == PamRotationJobAction.Claimed
                && x.Job.ExpiresAt > now
                && x.Job.ClaimedByAccessConnectorId == accessConnectorId
                && x.Job.ClaimedAt == x.Attempt.CreationDate)
            .Join(dbContext.PamRotationConfigs, x => x.Job.RotationConfigId, c => c.Id, (x, c) => new
            {
                c.CipherId,
                c.OrganizationId,
            })
            .FirstOrDefaultAsync();

        if (target is null)
        {
            await transaction.RollbackAsync();
            return PamRotationCipherWriteOutcome.Rejected;
        }

        var cipher = await dbContext.Ciphers.FirstOrDefaultAsync(c => c.Id == target.CipherId);
        if (cipher is null)
        {
            await transaction.RollbackAsync();
            return PamRotationCipherWriteOutcome.Rejected;
        }

        // A drifted revision date means a concurrent user edit; refused rather than clobbered. Tolerance mirrors CipherService.
        if (Math.Abs((cipher.RevisionDate - lastKnownRevisionDate).TotalMilliseconds) > 1000)
        {
            await transaction.RollbackAsync();
            return PamRotationCipherWriteOutcome.RevisionMismatch;
        }

        cipher.Data = cipherData;
        cipher.RevisionDate = now;

        var attempt = await dbContext.PamRotationAttempts.FirstAsync(a => a.Id == attemptId);
        attempt.CipherUpdated = true;

        await dbContext.SaveChangesAsync();

        // Every other writer of Cipher ends here: without the bump a client that misses the push sees an unchanged
        // AccountRevisionDate, skips the sync, and keeps serving the pre-rotation password.
        await dbContext.UserBumpAccountRevisionDateByCipherIdAsync(target.CipherId, target.OrganizationId);
        await dbContext.SaveChangesAsync();

        await transaction.CommitAsync();
        return PamRotationCipherWriteOutcome.Accepted;
    }

    public async Task<PamRotationAttemptResolveOutcome> MarkAttemptRotatedAsync(Guid attemptId, Guid accessConnectorId,
        PamSessionTerminationOutcome sessionTermination, DateTime now)
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
                && x.Job.ExpiresAt > now
                && x.Job.ClaimedByAccessConnectorId == accessConnectorId
                && x.Job.ClaimedAt == x.Attempt.CreationDate)
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
                .SetProperty(j => j.ClaimedAt, (DateTime?)null));

        await transaction.CommitAsync();
        return PamRotationAttemptResolveOutcome.Resolved;
    }

    public async Task<PamRotationFailureResult> MarkAttemptErroredAsync(Guid attemptId, Guid accessConnectorId,
        string? failureReason, PamRotationSyncState syncState, DateTime now, int maxAttempts, TimeSpan retryBaseDelay)
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
                && x.Job.ExpiresAt > now
                && x.Job.ClaimedByAccessConnectorId == accessConnectorId
                && x.Job.ClaimedAt == x.Attempt.CreationDate)
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

        // Abandoned attempts are deliberately not counted -- a release or timeout does not charge the retry budget.
        var erroredCount = await dbContext.PamRotationAttempts
            .CountAsync(a => a.JobId == jobId.Value && a.Action == PamRotationAttemptAction.Errored);

        // The status the job derives as right after this write; the guard saw it unexpired.
        PamRotationJobStatus jobStatus;
        if (erroredCount < maxAttempts)
        {
            jobStatus = PamRotationJobStatus.Pending;
            var backoff = TimeSpan.FromSeconds(retryBaseDelay.TotalSeconds * Math.Pow(2, erroredCount - 1));
            var nextClaimableAt = now + backoff;
            await dbContext.PamRotationJobs
                .Where(j => j.Id == jobId.Value)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Action, PamRotationJobAction.None)
                    .SetProperty(j => j.ClaimedByAccessConnectorId, (Guid?)null)
                    .SetProperty(j => j.ClaimedAt, (DateTime?)null)
                    .SetProperty(j => j.NextClaimableAt, nextClaimableAt));
        }
        else
        {
            jobStatus = PamRotationJobStatus.Failed;
            await dbContext.PamRotationJobs
                .Where(j => j.Id == jobId.Value)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Action, PamRotationJobAction.Failed)
                    .SetProperty(j => j.ClaimedByAccessConnectorId, (Guid?)null)
                    .SetProperty(j => j.ClaimedAt, (DateTime?)null));
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

        // A timeout is derived, not stored; PamRotationJobTimeoutSweep's journal decides which run owns a job.
        // No stronger isolation needed; a losing sweep's SaveChanges fails on the journal's primary key. A Rotated
        // attempt always comes with a Succeeded job, so success wins without checking attempts.
        var due = await dbContext.PamRotationJobs
            .Where(j => (j.Action == PamRotationJobAction.None || j.Action == PamRotationJobAction.Claimed)
                && j.ExpiresAt <= now
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
        await dbContext.SaveChangesAsync();

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
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var staleBefore = now - offlineAfter;

        // Releases only at lease expiry (not stale detection) to preserve success-wins; keyed on heartbeat staleness alone, so a disabled access connector's jobs still release.
        // A timed-out claim is the timeout sweep's, not a release; a Rotated attempt always comes with a Succeeded job.
        var candidates = await dbContext.PamRotationJobs
            .Where(j => j.Action == PamRotationJobAction.Claimed && j.ExpiresAt > now && j.ClaimedAt != null)
            .Join(dbContext.PamAccessConnectors, j => j.ClaimedByAccessConnectorId, d => d.Id,
                (j, d) => new { Job = j, AccessConnector = d })
            .Where(x => x.AccessConnector.LastHeartbeatAt == null || x.AccessConnector.LastHeartbeatAt < staleBefore)
            .Join(dbContext.PamRotationConfigs, x => x.Job.RotationConfigId, c => c.Id, (x, c) => new
            {
                JobId = x.Job.Id,
                x.Job.ClaimedAt,
                RotationConfigId = c.Id,
                c.OrganizationId,
                c.CipherId,
                x.Job.Source,
                PreviousClaimedByAccessConnectorId = x.Job.ClaimedByAccessConnectorId,
            })
            .AsNoTracking()
            .ToListAsync();

        // Computed in memory from the pre-clear ClaimedAt, since MySQL's UPDATE assigns left to right and would otherwise read the nulled column.
        var released = candidates
            .Where(c => c.ClaimedAt!.Value + releaseDelay <= now)
            .ToList();
        if (released.Count == 0)
        {
            await transaction.CommitAsync();
            return [];
        }

        foreach (var job in released)
        {
            var nextClaimableAt = job.ClaimedAt!.Value + releaseDelay;
            await dbContext.PamRotationJobs
                .Where(j => j.Id == job.JobId && j.Action == PamRotationJobAction.Claimed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Action, PamRotationJobAction.None)
                    .SetProperty(j => j.NextClaimableAt, nextClaimableAt)
                    .SetProperty(j => j.ClaimedByAccessConnectorId, (Guid?)null)
                    .SetProperty(j => j.ClaimedAt, (DateTime?)null));
        }

        // Records when the released claim's attempt ended; it derives as Abandoned, which the retry budget never
        // charges.
        await RecordReleasedAttemptsAsync(dbContext, released.Select(r => r.JobId).ToList(), now);

        await transaction.CommitAsync();

        return released
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
    /// The join set every eligibility decision shares: the job, its config and target, an assignment for this access
    /// connector, and -- defense in depth -- the access connector itself, enabled and in the config's organization.
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

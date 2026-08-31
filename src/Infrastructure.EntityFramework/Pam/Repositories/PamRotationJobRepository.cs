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

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Repositories;

/// <summary>
/// EF counterpart of the MSSQL rotation-job procedures. Those rely on <c>UPDLOCK, HOLDLOCK</c> range locks and the
/// <c>OUTPUT</c> clause, neither of which is portable, so the guarded transitions are rebuilt here from two
/// portable primitives: a serializable transaction where the MSSQL side takes a range lock, and a single
/// <c>ExecuteUpdate</c> whose <c>WHERE</c> carries the guard where the MSSQL side relies on a row lock. Every
/// invariant the procedures enforce — <c>AtMostOneActiveJobPerConfig</c>, <c>AtMostOneInFlightAttemptPerJob</c>,
/// first-claim-wins, and <c>VerifiedBeforeSuccess</c> — is enforced the same way here.
/// </summary>
public class PamRotationJobRepository : BaseEntityFrameworkRepository, IPamRotationJobRepository
{
    public PamRotationJobRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    { }

    public async Task<PamRotationJobCreateOutcome> CreateGuardedAsync(PamRotationJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // can_offer's eligibility half, re-checked here and not only by the caller, so a config disabled or a target
        // switched to Manual between the caller's read and this write cannot mint a job.
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

        // AtMostOneActiveJobPerConfig. Serializable holds the predicate's range for the life of the transaction, so
        // a concurrent create for the same config either blocks or fails to serialize rather than inserting a second.
        var hasActiveJob = await dbContext.PamRotationJobs
            .AnyAsync(j => j.RotationConfigId == job.RotationConfigId
                && (j.Status == PamRotationJobStatus.Pending || j.Status == PamRotationJobStatus.Claimed));
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

    public async Task<PamRotationClaimResult> ClaimAsync(Guid jobId, Guid daemonId, DateTime now, TimeSpan releaseDelay)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // First-claim-wins rests entirely on this statement: the Status == Pending predicate is evaluated under the
        // row lock the UPDATE itself takes, so two concurrent claims serialize and only the first flips the row.
        var claimed = await EligibleJobs(dbContext, daemonId)
            .Where(x => x.Job.Id == jobId
                && x.Job.Status == PamRotationJobStatus.Pending
                && x.Job.NextClaimableAt <= now
                && x.Config.Enabled
                && x.Target.Status == PamTargetSystemStatus.Active)
            .Select(x => x.Job)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, PamRotationJobStatus.Claimed)
                .SetProperty(j => j.ClaimedByDaemonId, daemonId)
                .SetProperty(j => j.ClaimedAt, now));

        if (claimed == 0)
        {
            // Classify eligibility first, so an unknown job and one this daemon may not claim produce the same
            // NotEligible outcome -- the caller maps it to 404, leaving no existence oracle.
            var eligible = await EligibleJobs(dbContext, daemonId).AnyAsync(x => x.Job.Id == jobId);
            await transaction.RollbackAsync();

            return new PamRotationClaimResult
            {
                Outcome = eligible ? PamRotationClaimOutcome.NotClaimable : PamRotationClaimOutcome.NotEligible,
            };
        }

        // AtMostOneInFlightAttemptPerJob: the Executing attempt is inserted in the claim's own transaction, so a
        // claimed job always has exactly one in-flight attempt from the moment it is claimed.
        var attempt = new EfAttempt
        {
            Id = CombGuid.Generate(),
            JobId = jobId,
            ClaimedByDaemonId = daemonId,
            CipherUpdated = false,
            Status = PamRotationAttemptStatus.Executing,
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

    public async Task<ICollection<PamClaimableJob>> GetManyClaimableByDaemonIdAsync(Guid daemonId, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Re-derives every condition ClaimAsync re-checks, so the list a daemon sees and what it can claim agree.
        return await EligibleJobs(dbContext, daemonId)
            .Where(x => x.Job.Status == PamRotationJobStatus.Pending
                && x.Job.NextClaimableAt <= now
                && x.Config.Enabled
                && x.Target.Status == PamTargetSystemStatus.Active)
            .Select(x => new PamClaimableJob
            {
                Id = x.Job.Id,
                RotationConfigId = x.Job.RotationConfigId,
                Source = x.Job.Source,
                Status = x.Job.Status,
                ClaimedByDaemonId = x.Job.ClaimedByDaemonId,
                ClaimedAt = x.Job.ClaimedAt,
                CreationDate = x.Job.CreationDate,
                NextClaimableAt = x.Job.NextClaimableAt,
                ExpiresAt = x.Job.ExpiresAt,
                TargetSystemId = x.Config.TargetSystemId,
            })
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ICollection<PamRotationJobDetails>> GetManyByConfigIdAsync(Guid configId)
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
                attemptsByJob.TryGetValue(job.Id, out var jobAttempts) ? jobAttempts : []))
            .ToList();
    }

    public async Task<ICollection<PamRotationJobDetails>> GetManyRecentByDaemonIdAsync(Guid daemonId, int limit)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var jobs = await dbContext.PamRotationJobs
            .Where(j => dbContext.PamRotationAttempts
                .Any(a => a.JobId == j.Id && a.ClaimedByDaemonId == daemonId))
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
            .Where(a => a.ClaimedByDaemonId == daemonId && jobIds.Contains(a.JobId))
            .OrderBy(a => a.CreationDate)
            .AsNoTracking()
            .ToListAsync();

        var attemptsByJob = attempts
            .GroupBy(a => a.JobId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PamRotationAttempt>)Mapper.Map<List<PamRotationAttempt>>(g.ToList()));

        return jobs
            .Select(job => PamRotationJobDetails.From(
                Mapper.Map<PamRotationJob>(job),
                attemptsByJob.TryGetValue(job.Id, out var jobAttempts) ? jobAttempts : []))
            .ToList();
    }

    public async Task<PamRotationAttempt?> GetAttemptByIdAsync(Guid attemptId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var attempt = await dbContext.PamRotationAttempts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attemptId);
        return Mapper.Map<PamRotationAttempt>(attempt);
    }

    public async Task<PamRotationCipherWriteOutcome> AcceptCipherWriteAsync(Guid attemptId, Guid daemonId,
        string cipherData, DateTime lastKnownRevisionDate, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // Serializable stands in for the MSSQL side's UPDLOCK on the job row: it keeps a concurrent release or
        // timeout sweep from moving the job between "is this attempt still allowed to write" and the write itself.
        var target = await dbContext.PamRotationAttempts
            .Where(a => a.Id == attemptId
                && a.Status == PamRotationAttemptStatus.Executing
                && a.ClaimedByDaemonId == daemonId)
            .Join(dbContext.PamRotationJobs, a => a.JobId, j => j.Id, (a, j) => new { Attempt = a, Job = j })
            .Where(x => x.Job.Status == PamRotationJobStatus.Claimed && x.Job.ClaimedByDaemonId == daemonId)
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

        // A drifted revision date means the vault item changed since the daemon last read it, so the write is
        // refused rather than clobbering a concurrent user edit. The one-second tolerance mirrors CipherService.
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

    public async Task<PamRotationAttemptResolveOutcome> MarkAttemptRotatedAsync(Guid attemptId, Guid daemonId,
        PamSessionTerminationOutcome sessionTermination, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // CipherUpdated is the VerifiedBeforeSuccess backstop: a daemon cannot report success for a rotation whose
        // new secret never reached the vault.
        var jobId = await dbContext.PamRotationAttempts
            .Where(a => a.Id == attemptId
                && a.Status == PamRotationAttemptStatus.Executing
                && a.ClaimedByDaemonId == daemonId
                && a.CipherUpdated)
            .Join(dbContext.PamRotationJobs, a => a.JobId, j => j.Id, (a, j) => j)
            .Where(j => j.Status == PamRotationJobStatus.Claimed)
            .Select(j => (Guid?)j.Id)
            .FirstOrDefaultAsync();

        if (jobId is null)
        {
            await transaction.RollbackAsync();
            return PamRotationAttemptResolveOutcome.Rejected;
        }

        await dbContext.PamRotationAttempts
            .Where(a => a.Id == attemptId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Status, PamRotationAttemptStatus.Rotated)
                .SetProperty(a => a.SessionTermination, sessionTermination)
                .SetProperty(a => a.ResolvedDate, now));

        await dbContext.PamRotationJobs
            .Where(j => j.Id == jobId.Value)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, PamRotationJobStatus.Succeeded)
                .SetProperty(j => j.ClaimedByDaemonId, (Guid?)null)
                .SetProperty(j => j.ClaimedAt, (DateTime?)null));

        await transaction.CommitAsync();
        return PamRotationAttemptResolveOutcome.Resolved;
    }

    public async Task<PamRotationFailureResult> MarkAttemptErroredAsync(Guid attemptId, Guid daemonId,
        string? failureReason, PamRotationSyncState syncState, DateTime now, int maxAttempts, TimeSpan retryBaseDelay)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var jobId = await dbContext.PamRotationAttempts
            .Where(a => a.Id == attemptId
                && a.Status == PamRotationAttemptStatus.Executing
                && a.ClaimedByDaemonId == daemonId)
            .Join(dbContext.PamRotationJobs, a => a.JobId, j => j.Id, (a, j) => j)
            .Where(j => j.Status == PamRotationJobStatus.Claimed)
            .Select(j => (Guid?)j.Id)
            .FirstOrDefaultAsync();

        if (jobId is null)
        {
            await transaction.RollbackAsync();
            return new PamRotationFailureResult { Outcome = PamRotationAttemptResolveOutcome.Rejected };
        }

        await dbContext.PamRotationAttempts
            .Where(a => a.Id == attemptId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Status, PamRotationAttemptStatus.Errored)
                .SetProperty(a => a.FailureReason, failureReason)
                .SetProperty(a => a.SyncState, syncState)
                .SetProperty(a => a.ResolvedDate, now));

        // Abandoned attempts are deliberately not counted -- a release or timeout does not charge the retry budget.
        var erroredCount = await dbContext.PamRotationAttempts
            .CountAsync(a => a.JobId == jobId.Value && a.Status == PamRotationAttemptStatus.Errored);

        PamRotationJobStatus jobStatus;
        if (erroredCount < maxAttempts)
        {
            jobStatus = PamRotationJobStatus.Pending;
            var backoff = TimeSpan.FromSeconds(retryBaseDelay.TotalSeconds * Math.Pow(2, erroredCount - 1));
            var nextClaimableAt = now + backoff;
            await dbContext.PamRotationJobs
                .Where(j => j.Id == jobId.Value)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, jobStatus)
                    .SetProperty(j => j.ClaimedByDaemonId, (Guid?)null)
                    .SetProperty(j => j.ClaimedAt, (DateTime?)null)
                    .SetProperty(j => j.NextClaimableAt, nextClaimableAt));
        }
        else
        {
            jobStatus = PamRotationJobStatus.Failed;
            await dbContext.PamRotationJobs
                .Where(j => j.Id == jobId.Value)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, jobStatus)
                    .SetProperty(j => j.ClaimedByDaemonId, (Guid?)null)
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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // EF has no OUTPUT clause, so the affected set is read first and the update is then keyed by those ids with
        // the same predicate re-applied. Serializable keeps another sweep from claiming the same rows in between.
        var due = await dbContext.PamRotationJobs
            .Where(j => (j.Status == PamRotationJobStatus.Pending || j.Status == PamRotationJobStatus.Claimed)
                && j.ExpiresAt <= now
                && !dbContext.PamRotationAttempts.Any(a => a.JobId == j.Id && a.Status == PamRotationAttemptStatus.Rotated))
            .Join(dbContext.PamRotationConfigs, j => j.RotationConfigId, c => c.Id, (j, c) => new
            {
                JobId = j.Id,
                RotationConfigId = c.Id,
                c.OrganizationId,
                c.CipherId,
                j.Source,
                PreviousClaimedByDaemonId = j.ClaimedByDaemonId,
            })
            .AsNoTracking()
            .ToListAsync();

        if (due.Count == 0)
        {
            await transaction.CommitAsync();
            return [];
        }

        var jobIds = due.Select(d => d.JobId).ToList();

        await dbContext.PamRotationJobs
            .Where(j => jobIds.Contains(j.Id)
                && (j.Status == PamRotationJobStatus.Pending || j.Status == PamRotationJobStatus.Claimed))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, PamRotationJobStatus.TimedOut)
                .SetProperty(j => j.ClaimedByDaemonId, (Guid?)null)
                .SetProperty(j => j.ClaimedAt, (DateTime?)null));

        await AbandonExecutingAttemptsAsync(dbContext, jobIds, now);

        var attemptCounts = await AttemptCountsAsync(dbContext, jobIds);

        await transaction.CommitAsync();

        return due
            .Select(d => new PamTimedOutJob
            {
                JobId = d.JobId,
                RotationConfigId = d.RotationConfigId,
                OrganizationId = d.OrganizationId,
                CipherId = d.CipherId,
                Source = d.Source,
                ClaimedByDaemonId = d.PreviousClaimedByDaemonId,
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

        // Releasing only at lease expiry -- not at stale detection -- preserves success-wins for a slow but live
        // daemon. Keyed on heartbeat staleness alone, never on daemon status, so a disabled daemon's jobs release.
        var candidates = await dbContext.PamRotationJobs
            .Where(j => j.Status == PamRotationJobStatus.Claimed && j.ClaimedAt != null)
            .Join(dbContext.PamDaemons, j => j.ClaimedByDaemonId, d => d.Id, (j, d) => new { Job = j, Daemon = d })
            .Where(x => (x.Daemon.LastHeartbeatAt == null || x.Daemon.LastHeartbeatAt < staleBefore)
                && !dbContext.PamRotationAttempts.Any(a => a.JobId == x.Job.Id && a.Status == PamRotationAttemptStatus.Rotated))
            .Join(dbContext.PamRotationConfigs, x => x.Job.RotationConfigId, c => c.Id, (x, c) => new
            {
                JobId = x.Job.Id,
                x.Job.ClaimedAt,
                RotationConfigId = c.Id,
                c.OrganizationId,
                c.CipherId,
                x.Job.Source,
                PreviousClaimedByDaemonId = x.Job.ClaimedByDaemonId,
            })
            .AsNoTracking()
            .ToListAsync();

        // The lease deadline is computed from the pre-clear ClaimedAt. Doing it in memory rather than in the UPDATE
        // keeps it correct on MySQL, whose UPDATE assigns left to right and would otherwise read the nulled column.
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
                .Where(j => j.Id == job.JobId && j.Status == PamRotationJobStatus.Claimed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, PamRotationJobStatus.Pending)
                    .SetProperty(j => j.NextClaimableAt, nextClaimableAt)
                    .SetProperty(j => j.ClaimedByDaemonId, (Guid?)null)
                    .SetProperty(j => j.ClaimedAt, (DateTime?)null));
        }

        await AbandonExecutingAttemptsAsync(dbContext, released.Select(r => r.JobId).ToList(), now);

        await transaction.CommitAsync();

        return released
            .Select(r => new PamReleasedJob
            {
                JobId = r.JobId,
                RotationConfigId = r.RotationConfigId,
                OrganizationId = r.OrganizationId,
                CipherId = r.CipherId,
                Source = r.Source,
                ClaimedByDaemonId = r.PreviousClaimedByDaemonId!.Value,
            })
            .ToList();
    }

    /// <remarks>
    /// The join set every eligibility decision shares: the job, its config and target, an assignment for this
    /// daemon, and -- defense in depth -- the daemon itself, enabled and in the config's organization.
    /// </remarks>
    private static IQueryable<EligibleJob> EligibleJobs(DatabaseContext dbContext, Guid daemonId) =>
        dbContext.PamRotationJobs
            .Join(dbContext.PamRotationConfigs, j => j.RotationConfigId, c => c.Id, (j, c) => new { Job = j, Config = c })
            .Join(dbContext.PamTargetSystems, x => x.Config.TargetSystemId, t => t.Id, (x, t) => new { x.Job, x.Config, Target = t })
            .Where(x => dbContext.PamDaemonTargetAssignments.Any(a =>
                a.DaemonId == daemonId && a.TargetSystemId == x.Config.TargetSystemId))
            .Join(dbContext.PamDaemons.Where(d => d.Id == daemonId && d.Status == PamAccessConnectorStatus.Enabled),
                x => x.Config.OrganizationId, d => d.OrganizationId,
                (x, d) => new EligibleJob { Job = x.Job, Config = x.Config, Target = x.Target });

    private static Task AbandonExecutingAttemptsAsync(DatabaseContext dbContext, List<Guid> jobIds, DateTime now) =>
        dbContext.PamRotationAttempts
            .Where(a => jobIds.Contains(a.JobId) && a.Status == PamRotationAttemptStatus.Executing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Status, PamRotationAttemptStatus.Abandoned)
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

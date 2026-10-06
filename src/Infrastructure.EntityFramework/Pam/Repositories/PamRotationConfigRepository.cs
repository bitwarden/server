using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreEntity = Bit.Pam.Entities.PamRotationConfig;
using EfModel = Bit.Infrastructure.EntityFramework.Pam.Models.PamRotationConfig;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Repositories;

public class PamRotationConfigRepository : Repository<CoreEntity, EfModel, Guid>, IPamRotationConfigRepository
{
    public PamRotationConfigRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.PamRotationConfigs)
    { }

    public async Task<CoreEntity?> GetByCipherIdAsync(Guid cipherId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var config = await dbContext.PamRotationConfigs
            .Where(c => c.CipherId == cipherId)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return Mapper.Map<CoreEntity>(config);
    }

    public async Task<PamRotationConfigDetails?> GetDetailsByIdAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await DetailsQuery(dbContext).Where(c => c.Id == id).FirstOrDefaultAsync();
    }

    public async Task<ICollection<PamRotationConfigDetails>> GetManyDetailsByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await DetailsQuery(dbContext).Where(c => c.OrganizationId == organizationId).ToListAsync();
    }

    public async Task<ICollection<CoreEntity>> GetManyDueAsync(DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var configs = await dbContext.PamRotationConfigs
            .Join(dbContext.PamTargetSystems, c => c.TargetSystemId, t => t.Id, (c, t) => new { Config = c, Target = t })
            .Where(x => x.Config.Enabled
                && x.Config.NextRotationAt != null
                && x.Config.NextRotationAt <= now
                && x.Target.Method == PamTargetSystemMethod.Automatic
                && x.Target.Status == PamTargetSystemStatus.Active
                // OfferRotation is the single creation point; a config already carrying work is not offered again.
                // Until the timeout sweep records a timed-out job it still blocks, so its reschedule is never raced.
                && !dbContext.PamRotationJobs.Any(j => j.RotationConfigId == x.Config.Id
                    && (j.Action == PamRotationJobAction.None || j.Action == PamRotationJobAction.Claimed)
                    && !dbContext.PamRotationJobTimeoutSweeps.Any(s => s.RotationJobId == j.Id)))
            .Select(x => x.Config)
            .AsNoTracking()
            .ToListAsync();

        return Mapper.Map<List<CoreEntity>>(configs);
    }

    public async Task<bool> AnyByTargetSystemAsync(Guid targetSystemId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await dbContext.PamRotationConfigs.AnyAsync(c => c.TargetSystemId == targetSystemId);
    }

    public async Task<bool> AnyByTargetSystemWithTerminateSessionsAsync(Guid targetSystemId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await dbContext.PamRotationConfigs
            .AnyAsync(c => c.TargetSystemId == targetSystemId && c.TerminateSessions);
    }

    public async Task<bool> DeleteWithJobsAsync(Guid configId)
        => await SerializableRetry.RunAsync(() => DeleteGuardedAsync(configId));

    private async Task<bool> DeleteGuardedAsync(Guid configId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Serializable makes the active-job re-check and the deletes one indivisible step, so a job offered in
        // between can't be torn out from under its access connector. A loser aborted at commit is replayed by
        // SerializableRetry.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        var hasActiveJob = await dbContext.PamRotationJobs
            .AnyAsync(j => j.RotationConfigId == configId
                && (j.Action == PamRotationJobAction.None || j.Action == PamRotationJobAction.Claimed)
                && !dbContext.PamRotationJobTimeoutSweeps.Any(s => s.RotationJobId == j.Id));
        if (hasActiveJob)
        {
            await transaction.RollbackAsync();
            return false;
        }

        // Attempts reference jobs and jobs reference the config, both NO ACTION, so children go first. The timeout
        // journal cascades from the jobs on its own.
        var jobIds = await dbContext.PamRotationJobs
            .Where(j => j.RotationConfigId == configId)
            .Select(j => j.Id)
            .ToListAsync();

        if (jobIds.Count > 0)
        {
            await dbContext.PamRotationAttempts.Where(a => jobIds.Contains(a.JobId)).ExecuteDeleteAsync();
            await dbContext.PamRotationJobs.Where(j => jobIds.Contains(j.Id)).ExecuteDeleteAsync();
        }

        await dbContext.PamRotationConfigs.Where(c => c.Id == configId).ExecuteDeleteAsync();

        await transaction.CommitAsync();
        return true;
    }

    private static IQueryable<PamRotationConfigDetails> DetailsQuery(DatabaseContext dbContext) =>
        dbContext.PamRotationConfigs
            .Join(dbContext.PamTargetSystems, c => c.TargetSystemId, t => t.Id, (c, t) => new PamRotationConfigDetails
            {
                Id = c.Id,
                OrganizationId = c.OrganizationId,
                CipherId = c.CipherId,
                TargetSystemId = c.TargetSystemId,
                AccountIdentity = c.AccountIdentity,
                TerminateSessions = c.TerminateSessions,
                ScheduleCron = c.ScheduleCron,
                RotateOnAccessEnd = c.RotateOnAccessEnd,
                NextRotationAt = c.NextRotationAt,
                Enabled = c.Enabled,
                LastRotationAt = c.LastRotationAt,
                CreationDate = c.CreationDate,
                RevisionDate = c.RevisionDate,
                TargetSystemName = t.Name,
                TargetSystemMethod = t.Method,
                // A timed-out job stays active until the timeout sweep records it.
                HasActiveJob = dbContext.PamRotationJobs.Any(j => j.RotationConfigId == c.Id
                    && (j.Action == PamRotationJobAction.None || j.Action == PamRotationJobAction.Claimed)
                    && !dbContext.PamRotationJobTimeoutSweeps.Any(s => s.RotationJobId == j.Id)),
            })
            .AsNoTracking();
}

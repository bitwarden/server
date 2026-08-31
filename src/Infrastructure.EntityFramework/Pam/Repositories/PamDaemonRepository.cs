using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreAssignment = Bit.Pam.Entities.PamDaemonTargetAssignment;
using CoreEntity = Bit.Pam.Entities.PamDaemon;
using EfAssignment = Bit.Infrastructure.EntityFramework.Pam.Models.PamDaemonTargetAssignment;
using EfModel = Bit.Infrastructure.EntityFramework.Pam.Models.PamDaemon;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Repositories;

public class PamDaemonRepository : Repository<CoreEntity, EfModel, Guid>, IPamDaemonRepository
{
    public PamDaemonRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.PamDaemons)
    { }

    public async Task<ICollection<CoreEntity>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var daemons = await dbContext.PamDaemons
            .Where(d => d.OrganizationId == organizationId)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreEntity>>(daemons);
    }

    public async Task<PamDaemonDetails?> GetDetailsByApiKeyIdAsync(Guid apiKeyId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // The organization's licensing state travels with the daemon so the token path resolves both in one read.
        return await dbContext.PamDaemons
            .Where(d => d.ApiKeyId == apiKeyId)
            .Join(dbContext.Organizations, d => d.OrganizationId, o => o.Id, (d, o) => new PamDaemonDetails
            {
                Id = d.Id,
                OrganizationId = d.OrganizationId,
                Name = d.Name,
                ApiKeyId = d.ApiKeyId,
                Status = d.Status,
                LastHeartbeatAt = d.LastHeartbeatAt,
                CreationDate = d.CreationDate,
                RevisionDate = d.RevisionDate,
                OrganizationEnabled = o.Enabled,
                OrganizationUsePam = o.UsePam,
            })
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    /// <remarks>
    /// Narrowed to the same three columns PamDaemon_Update writes. ApiKeyId is set once at registration and
    /// OrganizationId never changes, so persisting a caller-mutated value would let an admin edit move a daemon
    /// between organizations; LastHeartbeatAt has its own conditional-bump path so a routine edit never races the
    /// daemon's own poll. The generic whole-entity replace would write all of them.
    /// </remarks>
    public override async Task ReplaceAsync(CoreEntity obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await dbContext.PamDaemons
            .Where(d => d.Id == obj.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.Name, obj.Name)
                .SetProperty(d => d.Status, obj.Status)
                .SetProperty(d => d.RevisionDate, obj.RevisionDate));
    }

    public async Task UpdateHeartbeatAsync(Guid daemonId, DateTime now, TimeSpan minInterval)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Conditional in the predicate rather than read-then-write, so a tightly polling daemon issues one statement
        // and concurrent requests cannot each decide the value is stale.
        var staleBefore = now - minInterval;
        await dbContext.PamDaemons
            .Where(d => d.Id == daemonId && (d.LastHeartbeatAt == null || d.LastHeartbeatAt < staleBefore))
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.LastHeartbeatAt, now));
    }

    /// <remarks>
    /// Mirrors PamDaemon_DeleteById: a job still claimed by this daemon has to be released before the daemon row
    /// goes, because the release sweep finds stale claimants by joining PamDaemon and would never see it again.
    /// </remarks>
    public override async Task DeleteAsync(CoreEntity obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var now = DateTime.UtcNow;
        // obj.ApiKeyId is not trusted here -- the stored row decides which credential goes.
        var apiKeyId = await dbContext.PamDaemons
            .Where(d => d.Id == obj.Id)
            .Select(d => d.ApiKeyId)
            .FirstOrDefaultAsync();

        var claimedJobIds = await dbContext.PamRotationJobs
            .Where(j => j.ClaimedByDaemonId == obj.Id && j.Status == PamRotationJobStatus.Claimed)
            .Select(j => j.Id)
            .ToListAsync();

        if (claimedJobIds.Count > 0)
        {
            await dbContext.PamRotationAttempts
                .Where(a => claimedJobIds.Contains(a.JobId) && a.Status == PamRotationAttemptStatus.Executing)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(a => a.Status, PamRotationAttemptStatus.Abandoned)
                    .SetProperty(a => a.ResolvedDate, now));

            await dbContext.PamRotationJobs
                .Where(j => claimedJobIds.Contains(j.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, PamRotationJobStatus.Pending)
                    .SetProperty(j => j.ClaimedByDaemonId, (Guid?)null)
                    .SetProperty(j => j.ClaimedAt, (DateTime?)null)
                    .SetProperty(j => j.NextClaimableAt, now));
        }

        // The assignment -> daemon FK is NO ACTION, so assignments must go before the daemon row.
        await dbContext.PamDaemonTargetAssignments
            .Where(a => a.DaemonId == obj.Id)
            .ExecuteDeleteAsync();

        await dbContext.PamDaemons.Where(d => d.Id == obj.Id).ExecuteDeleteAsync();

        // The daemon -> ApiKey FK is NO ACTION as well, so the credential goes last.
        if (apiKeyId != Guid.Empty)
        {
            await dbContext.ApiKeys.Where(k => k.Id == apiKeyId).ExecuteDeleteAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task CreateAssignmentAsync(CoreAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var entity = Mapper.Map<EfAssignment>(assignment);
        await dbContext.PamDaemonTargetAssignments.AddAsync(entity);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAssignmentAsync(Guid daemonId, Guid targetSystemId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await dbContext.PamDaemonTargetAssignments
            .Where(a => a.DaemonId == daemonId && a.TargetSystemId == targetSystemId)
            .ExecuteDeleteAsync();
    }

    public async Task<ICollection<CoreAssignment>> GetAssignmentsByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var assignments = await dbContext.PamDaemonTargetAssignments
            .Where(a => a.OrganizationId == organizationId)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreAssignment>>(assignments);
    }

    public async Task<bool> AssignmentExistsAsync(Guid daemonId, Guid targetSystemId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await dbContext.PamDaemonTargetAssignments
            .AnyAsync(a => a.DaemonId == daemonId && a.TargetSystemId == targetSystemId);
    }
}

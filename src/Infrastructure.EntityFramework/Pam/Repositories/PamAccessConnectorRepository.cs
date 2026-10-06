using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreAssignment = Bit.Pam.Entities.PamAccessConnectorTargetAssignment;
using CoreEntity = Bit.Pam.Entities.PamAccessConnector;
using EfAssignment = Bit.Infrastructure.EntityFramework.Pam.Models.PamAccessConnectorTargetAssignment;
using EfModel = Bit.Infrastructure.EntityFramework.Pam.Models.PamAccessConnector;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Repositories;

public class PamAccessConnectorRepository : Repository<CoreEntity, EfModel, Guid>, IPamAccessConnectorRepository
{
    public PamAccessConnectorRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.PamAccessConnectors)
    { }

    public async Task<ICollection<CoreEntity>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var accessConnectors = await dbContext.PamAccessConnectors
            .Where(d => d.OrganizationId == organizationId)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreEntity>>(accessConnectors);
    }

    public async Task<PamAccessConnectorDetails?> GetDetailsByApiKeyIdAsync(Guid apiKeyId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // The organization's licensing state travels with the access connector so the token path resolves both in one
        // read.
        return await dbContext.PamAccessConnectors
            .Where(d => d.ApiKeyId == apiKeyId)
            .Join(dbContext.Organizations, d => d.OrganizationId, o => o.Id, (d, o) => new PamAccessConnectorDetails
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
    /// Narrowed to the same three columns PamAccessConnector_Update writes: ApiKeyId and OrganizationId must not move
    /// via a whole-entity replace, and LastHeartbeatAt has its own conditional-bump path so a routine edit doesn't race
    /// the access connector's poll.
    /// </remarks>
    public override async Task ReplaceAsync(CoreEntity obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await dbContext.PamAccessConnectors
            .Where(d => d.Id == obj.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.Name, obj.Name)
                .SetProperty(d => d.Status, obj.Status)
                .SetProperty(d => d.RevisionDate, obj.RevisionDate));
    }

    public async Task UpdateHeartbeatAsync(Guid accessConnectorId, DateTime now, TimeSpan minInterval)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Conditional in the predicate rather than read-then-write, so a tightly polling access connector issues one
        // statement and concurrent requests cannot each decide the value is stale.
        var staleBefore = now - minInterval;
        await dbContext.PamAccessConnectors
            .Where(d => d.Id == accessConnectorId && (d.LastHeartbeatAt == null || d.LastHeartbeatAt < staleBefore))
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.LastHeartbeatAt, now));
    }

    /// <remarks>
    /// Mirrors PamAccessConnector_DeleteById: releases the access connector's claimed jobs first, since the release
    /// sweep finds stale claimants by joining PamAccessConnector and would miss them once the row is gone.
    /// </remarks>
    public override async Task DeleteAsync(CoreEntity obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var now = DateTime.UtcNow;
        // obj.ApiKeyId is not trusted here -- the stored row decides which credential goes.
        var apiKeyId = await dbContext.PamAccessConnectors
            .Where(d => d.Id == obj.Id)
            .Select(d => d.ApiKeyId)
            .FirstOrDefaultAsync();

        // Releases the access connector's live claims; their attempts derive as Abandoned, so only the end is
        // recorded. A timed-out claim is left as it was.
        var claimedJobIds = await dbContext.PamRotationJobs
            .Where(j => j.ClaimedByAccessConnectorId == obj.Id
                && j.Action == PamRotationJobAction.Claimed
                && j.ExpiresAt > now)
            .Select(j => j.Id)
            .ToListAsync();

        if (claimedJobIds.Count > 0)
        {
            await dbContext.PamRotationAttempts
                .Where(a => claimedJobIds.Contains(a.JobId)
                    && a.Action == PamRotationAttemptAction.None
                    && a.ResolvedDate == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(a => a.ResolvedDate, now));

            await dbContext.PamRotationJobs
                .Where(j => claimedJobIds.Contains(j.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Action, PamRotationJobAction.None)
                    .SetProperty(j => j.ClaimedByAccessConnectorId, (Guid?)null)
                    .SetProperty(j => j.ClaimedAt, (DateTime?)null)
                    .SetProperty(j => j.NextClaimableAt, now));
        }

        // The assignment -> access connector FK is NO ACTION, so assignments must go before the access connector row.
        await dbContext.PamAccessConnectorTargetAssignments
            .Where(a => a.AccessConnectorId == obj.Id)
            .ExecuteDeleteAsync();

        await dbContext.PamAccessConnectors.Where(d => d.Id == obj.Id).ExecuteDeleteAsync();

        // The access connector -> ApiKey FK is NO ACTION as well, so the credential goes last.
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
        await dbContext.PamAccessConnectorTargetAssignments.AddAsync(entity);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAssignmentAsync(Guid accessConnectorId, Guid targetSystemId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await dbContext.PamAccessConnectorTargetAssignments
            .Where(a => a.AccessConnectorId == accessConnectorId && a.TargetSystemId == targetSystemId)
            .ExecuteDeleteAsync();
    }

    public async Task<ICollection<CoreAssignment>> GetAssignmentsByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var assignments = await dbContext.PamAccessConnectorTargetAssignments
            .Where(a => a.OrganizationId == organizationId)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreAssignment>>(assignments);
    }

    public async Task<bool> AssignmentExistsAsync(Guid accessConnectorId, Guid targetSystemId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await dbContext.PamAccessConnectorTargetAssignments
            .AnyAsync(a => a.AccessConnectorId == accessConnectorId && a.TargetSystemId == targetSystemId);
    }
}

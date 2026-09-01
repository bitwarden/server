using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Pam.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreEntity = Bit.Pam.Entities.PamTargetSystem;
using EfModel = Bit.Infrastructure.EntityFramework.Pam.Models.PamTargetSystem;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Repositories;

public class PamTargetSystemRepository : Repository<CoreEntity, EfModel, Guid>, IPamTargetSystemRepository
{
    public PamTargetSystemRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.PamTargetSystems)
    { }

    public async Task<ICollection<CoreEntity>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        // Unordered, matching PamTargetSystem_ReadByOrganizationId -- callers sort for display.
        var targets = await dbContext.PamTargetSystems
            .Where(t => t.OrganizationId == organizationId)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreEntity>>(targets);
    }

    /// <remarks>
    /// Mirrors PamTargetSystem_DeleteWithAssignments. The assignment -> target FK is NO ACTION, so the assignments
    /// go before the target row; a rotation config naming the target refuses the delete instead of cascading, since
    /// it is the configuration for a credential rather than an edge between two rows.
    /// </remarks>
    public async Task<bool> DeleteWithAssignmentsAsync(Guid targetSystemId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Serializable so the config re-check and the deletes are one indivisible step: a config created between
        // them would otherwise be left naming a target that no longer exists.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        var hasConfig = await dbContext.PamRotationConfigs.AnyAsync(c => c.TargetSystemId == targetSystemId);
        if (hasConfig)
        {
            await transaction.RollbackAsync();
            return false;
        }

        await dbContext.PamDaemonTargetAssignments
            .Where(a => a.TargetSystemId == targetSystemId)
            .ExecuteDeleteAsync();

        await dbContext.PamTargetSystems.Where(t => t.Id == targetSystemId).ExecuteDeleteAsync();

        await transaction.CommitAsync();
        return true;
    }
}

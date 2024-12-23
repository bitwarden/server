using AutoMapper;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Vault.Models;
using Bit.Infrastructure.EntityFramework.Vault.Repositories.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories;

public class SecurityTaskRepository : Repository<Core.Vault.Entities.SecurityTask, SecurityTask, Guid>, ISecurityTaskRepository
{
    public SecurityTaskRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (context) => context.SecurityTasks)
    { }

    /// <inheritdoc />
    public async Task<ICollection<Core.Vault.Entities.SecurityTask>> GetManyByUserIdStatusAsync(Guid userId,
        SecurityTaskStatus? status = null)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = new SecurityTaskReadByUserIdStatusQuery(userId, status);
        var data = await query.Run(dbContext).ToListAsync();
        return data;
    }

    /// <inheritdoc />
    public async Task<ICollection<Core.Vault.Entities.SecurityTask>> CreateManyAsync(
        IEnumerable<Core.Vault.Entities.SecurityTask> tasks)
    {
        var tasksList = tasks?.ToList();
        if (tasksList is null || tasksList.Count == 0)
        {
            return Array.Empty<SecurityTask>();
        }

        foreach (var task in tasksList)
        {
            task.SetNewId();
        }

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var entities = Mapper.Map<List<SecurityTask>>(tasksList);
        await dbContext.AddRangeAsync(entities);
        await dbContext.SaveChangesAsync();

        return tasksList;
    }
}

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
    public async Task<ICollection<Core.Vault.Entities.SecurityTask>> GetManyByOrganizationIdStatusAsync(Guid organizationId,
        SecurityTaskStatus? status = null)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = from st in dbContext.SecurityTasks
                    join o in dbContext.Organizations
                        on st.OrganizationId equals o.Id
                    where
                        o.Enabled &&
                        st.OrganizationId == organizationId &&
                        (status == null || st.Status == status)
                    select new Core.Vault.Entities.SecurityTask
                    {
                        Id = st.Id,
                        OrganizationId = st.OrganizationId,
                        CipherId = st.CipherId,
                        Status = st.Status,
                        Type = st.Type,
                        CreationDate = st.CreationDate,
                        RevisionDate = st.RevisionDate,
                    };

        return await query.OrderByDescending(st => st.CreationDate).ToListAsync();
    }
}

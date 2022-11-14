using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using User = Bit.Core.Entities.User;

namespace Bit.Commercial.Infrastructure.EntityFramework.Repositories;

public class ProjectRepository : Repository<Core.Entities.Project, Project, Guid>, IProjectRepository
{
    public ProjectRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, db => db.Project)
    { }

    public override async Task<Core.Entities.Project> GetByIdAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var project = await dbContext.Project
                                    .Where(c => c.Id == id && c.DeletedDate == null)
                                    .FirstOrDefaultAsync();
            return Mapper.Map<Core.Entities.Project>(project);
        }
    }

    public async Task<IEnumerable<Core.Entities.Project>> GetManyByOrganizationIdAsync(Guid organizationId,
        Core.Entities.User user)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var project = await dbContext.Project
            .Where(p => p.OrganizationId == organizationId &&
                        (
                            p.UserAccessPolicies.Any(IsUser(user)) ||
                            p.GroupAccessPolicies.Any(IsGroupUser(user))
                        ) &&
                        p.DeletedDate == null)
            .OrderBy(p => p.RevisionDate)
            .ToListAsync();
        return Mapper.Map<List<Core.Entities.Project>>(project);
    }

    private static Func<GroupProjectAccessPolicy, bool> IsGroupUser(User user) =>
        ap => ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == user.Id);

    private static Func<UserProjectAccessPolicy, bool> IsUser(User user) =>
        ap => ap.OrganizationUser.User.Id == user.Id;

    public async Task DeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var utcNow = DateTime.UtcNow;
            var projects = dbContext.Project.Where(c => ids.Contains(c.Id));
            await projects.ForEachAsync(project =>
            {
                dbContext.Remove(project);
            });
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Core.Entities.Project>> GetManyByIds(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var projects = await dbContext.Project
                                    .Where(c => ids.Contains(c.Id) && c.DeletedDate == null)
                                    .ToListAsync();
            return Mapper.Map<List<Core.Entities.Project>>(projects);
        }

    }
}

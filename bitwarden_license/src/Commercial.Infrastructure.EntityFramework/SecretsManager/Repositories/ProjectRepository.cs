using System.Linq.Expressions;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.SecretsManager.Repositories;

public class ProjectRepository : Repository<Core.SecretsManager.Entities.Project, Project, Guid>, IProjectRepository
{
    public ProjectRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, db => db.Project)
    { }

    public override async Task<Core.SecretsManager.Entities.Project> GetByIdAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var project = await dbContext.Project
                                    .Where(c => c.Id == id && c.DeletedDate == null)
                                    .FirstOrDefaultAsync();
            return Mapper.Map<Core.SecretsManager.Entities.Project>(project);
        }
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.Project>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Project.Where(p => p.OrganizationId == organizationId && p.DeletedDate == null);

        query = accessType switch
        {
            AccessClientType.NoAccessCheck => query,
            AccessClientType.User => query.Where(UserHasReadAccessToProject(userId)),
            AccessClientType.ServiceAccount => query.Where(ServiceAccountHasReadAccessToProject(userId)),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };

        var projects = await query.OrderBy(p => p.RevisionDate).ToListAsync();
        return Mapper.Map<List<Core.SecretsManager.Entities.Project>>(projects);
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.Project>> GetManyByOrganizationIdWriteAccessAsync(
        Guid organizationId, Guid userId, AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Project.Where(p => p.OrganizationId == organizationId && p.DeletedDate == null);

        query = accessType switch
        {
            AccessClientType.NoAccessCheck => query,
            AccessClientType.User => query.Where(UserHasWriteAccessToProject(userId)),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };

        var projects = await query.OrderBy(p => p.RevisionDate).ToListAsync();
        return Mapper.Map<List<Core.SecretsManager.Entities.Project>>(projects);
    }

    private static Expression<Func<Project, bool>> UserHasReadAccessToProject(Guid userId) => p =>
        p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Read) ||
        p.GroupAccessPolicies.Any(ap => ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Read));

    private static Expression<Func<Project, bool>> UserHasWriteAccessToProject(Guid userId) => p =>
        p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
        p.GroupAccessPolicies.Any(ap => ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write));

    private static Expression<Func<Project, bool>> ServiceAccountHasReadAccessToProject(Guid serviceAccountId) => p =>
        p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccount.Id == serviceAccountId && ap.Read);

    private static Expression<Func<Project, bool>> ServiceAccountHasWriteAccessToProject(Guid serviceAccountId) => p =>
        p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccount.Id == serviceAccountId && ap.Write);

    public async Task DeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var projects = dbContext.Project.Where(c => ids.Contains(c.Id));
            await projects.ForEachAsync(project =>
            {
                dbContext.Remove(project);
            });
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.Project>> GetManyWithSecretsByIds(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var projects = await dbContext.Project
                                    .Include(p => p.Secrets)
                                    .Where(c => ids.Contains(c.Id) && c.DeletedDate == null)
                                    .ToListAsync();
            return Mapper.Map<List<Core.SecretsManager.Entities.Project>>(projects);
        }
    }

    public async Task<bool> ServiceAccountHasReadAccessToProject(Guid id, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Project
            .Where(p => p.Id == id)
            .Where(ServiceAccountHasReadAccessToProject(userId));

        return await query.AnyAsync();
    }

    public async Task<bool> ServiceAccountHasWriteAccessToProject(Guid id, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Project
            .Where(p => p.Id == id)
            .Where(ServiceAccountHasWriteAccessToProject(userId));

        return await query.AnyAsync();
    }

    public async Task<bool> UserHasReadAccessToProject(Guid id, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Project
            .Where(p => p.Id == id)
            .Where(UserHasReadAccessToProject(userId));

        return await query.AnyAsync();
    }

    public async Task<bool> UserHasWriteAccessToProject(Guid id, Guid userId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Project
            .Where(p => p.Id == id)
            .Where(UserHasWriteAccessToProject(userId));

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.Project>> ImportAsync(IEnumerable<Core.SecretsManager.Entities.Project> projects)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var entities = projects.Select(p => Mapper.Map<Project>(p));
        var dbContext = GetDatabaseContext(scope);
        await GetDbSet(dbContext).AddRangeAsync(entities);
        await dbContext.SaveChangesAsync();
        return projects;
    }

    public async Task<(bool Read, bool Write)> AccessToProjectAsync(Guid id, Guid userId, AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var projectQuery = dbContext.Project
            .Where(s => s.Id == id);

        var query = accessType switch
        {
            AccessClientType.NoAccessCheck => projectQuery.Select(_ => new { Read = true, Write = true }),
            AccessClientType.User => projectQuery.Select(p => new
            {
                Read = p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Read)
                       || p.GroupAccessPolicies.Any(ap =>
                           ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Read)),
                Write = p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
                        p.GroupAccessPolicies.Any(ap =>
                            ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write)),
            }),
            AccessClientType.ServiceAccount => projectQuery.Select(p => new
            {
                Read = p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccountId == userId && ap.Read),
                Write = p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccountId == userId && ap.Write),
            }),
            _ => projectQuery.Select(_ => new { Read = false, Write = false }),
        };

        var policy = await query.FirstOrDefaultAsync();

        return (policy.Read, policy.Write);
    }
}

﻿using System.Linq.Expressions;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Models.Data;
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

    public override async Task<Core.SecretsManager.Entities.Project?> GetByIdAsync(Guid id)
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

    public async Task<IEnumerable<ProjectPermissionDetails>> GetManyByOrganizationIdAsync(
        Guid organizationId,
        Guid userId,
        AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var query = dbContext.Project.Where(p => p.OrganizationId == organizationId && p.DeletedDate == null).OrderBy(p => p.RevisionDate);

        var projects = ProjectToPermissionDetails(query, userId, accessType);

        return await projects.ToListAsync();
    }

    public async Task<int> GetProjectCountByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return await dbContext.Project
                .CountAsync(ou => ou.OrganizationId == organizationId);
        }
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

    public async Task DeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var serviceAccountIds = await dbContext.Project
            .Where(p => ids.Contains(p.Id))
            .Include(p => p.ServiceAccountAccessPolicies)
            .SelectMany(p => p.ServiceAccountAccessPolicies.Select(ap => ap.ServiceAccountId!.Value))
            .Distinct()
            .ToListAsync();

        var secretIds = await dbContext.Project
            .Where(p => ids.Contains(p.Id))
            .Include(p => p.Secrets)
            .SelectMany(p => p.Secrets.Select(s => s.Id))
            .Distinct()
            .ToListAsync();

        var utcNow = DateTime.UtcNow;
        if (serviceAccountIds.Count > 0)
        {
            await dbContext.ServiceAccount
                .Where(sa => serviceAccountIds.Contains(sa.Id))
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(sa => sa.RevisionDate, utcNow));
        }

        if (secretIds.Count > 0)
        {
            await dbContext.Secret
                .Where(s => secretIds.Contains(s.Id))
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(s => s.RevisionDate, utcNow));
        }

        await dbContext.Project.Where(p => ids.Contains(p.Id)).ExecuteDeleteAsync();
        await transaction.CommitAsync();
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

        var accessQuery = BuildProjectAccessQuery(projectQuery, userId, accessType);
        var policy = await accessQuery.FirstOrDefaultAsync();

        return policy == null ? (false, false) : (policy.Read, policy.Write);
    }

    public async Task<bool> ProjectsAreInOrganization(List<Guid> projectIds, Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var results = await dbContext.Project.Where(p => p.OrganizationId == organizationId && projectIds.Contains(p.Id)).ToListAsync();

        return projectIds.Count == results.Count;
    }

    public async Task<Dictionary<Guid, (bool Read, bool Write)>> AccessToProjectsAsync(
        IEnumerable<Guid> projectIds,
        Guid userId,
        AccessClientType accessType)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);

        var projectsQuery = dbContext.Project.Where(p => projectIds.Contains(p.Id));
        var accessQuery = BuildProjectAccessQuery(projectsQuery, userId, accessType);

        return await accessQuery.ToDictionaryAsync(pa => pa.Id, pa => (pa.Read, pa.Write));
    }

    public async Task<int> GetProjectCountByOrganizationIdAsync(Guid organizationId, Guid userId,
        AccessClientType accessType)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Project.Where(p => p.OrganizationId == organizationId && p.DeletedDate == null);

        query = accessType switch
        {
            AccessClientType.NoAccessCheck => query,
            AccessClientType.User => query.Where(UserHasReadAccessToProject(userId)),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };

        return await query.CountAsync();
    }

    public async Task<ProjectCounts> GetProjectCountsByIdAsync(Guid projectId, Guid userId, AccessClientType accessType)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Project.Where(p => p.Id == projectId && p.DeletedDate == null);

        var queryReadAccess = accessType switch
        {
            AccessClientType.NoAccessCheck => query,
            AccessClientType.User => query.Where(UserHasReadAccessToProject(userId)),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };

        var queryWriteAccess = accessType switch
        {
            AccessClientType.NoAccessCheck => query,
            AccessClientType.User => query.Where(UserHasWriteAccessToProject(userId)),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };

        var secretsQuery = queryReadAccess.Select(project => project.Secrets.Count(s => s.DeletedDate == null));

        var projectCountsQuery = queryWriteAccess.Select(project => new ProjectCounts
        {
            People = project.UserAccessPolicies.Count + project.GroupAccessPolicies.Count,
            ServiceAccounts = project.ServiceAccountAccessPolicies.Count
        });

        var secrets = await secretsQuery.FirstOrDefaultAsync();
        var projectCounts = await projectCountsQuery.FirstOrDefaultAsync() ?? new ProjectCounts { Secrets = 0, People = 0, ServiceAccounts = 0 };
        projectCounts.Secrets = secrets;

        return projectCounts;
    }

    private record ProjectAccess(Guid Id, bool Read, bool Write);

    private static IQueryable<ProjectAccess> BuildProjectAccessQuery(IQueryable<Project> projectQuery, Guid userId,
        AccessClientType accessType) =>
        accessType switch
        {
            AccessClientType.NoAccessCheck => projectQuery.Select(p => new ProjectAccess(p.Id, true, true)),
            AccessClientType.User => projectQuery.Select(p => new ProjectAccess
            (
                p.Id,
                p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Read) ||
                p.GroupAccessPolicies.Any(ap =>
                    ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Read)),
                p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
                p.GroupAccessPolicies.Any(ap =>
                    ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write))
            )),
            AccessClientType.ServiceAccount => projectQuery.Select(p => new ProjectAccess
            (
                p.Id,
                p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccountId == userId && ap.Read),
                p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccountId == userId && ap.Write)
            )),
            _ => projectQuery.Select(p => new ProjectAccess(p.Id, false, false))
        };

    private IQueryable<ProjectPermissionDetails> ProjectToPermissionDetails(IQueryable<Project> query, Guid userId, AccessClientType accessType)
    {
        var projects = accessType switch
        {
            AccessClientType.NoAccessCheck => query.Select(p => new ProjectPermissionDetails
            {
                Project = Mapper.Map<Bit.Core.SecretsManager.Entities.Project>(p),
                Read = true,
                Write = true,
            }),
            AccessClientType.User => query.Where(UserHasReadAccessToProject(userId)).Select(ProjectToPermissionsUser(userId, true)),
            AccessClientType.ServiceAccount => query.Where(ServiceAccountHasReadAccessToProject(userId)).Select(ProjectToPermissionsServiceAccount(userId, true)),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };
        return projects;
    }

    private Expression<Func<Project, ProjectPermissionDetails>> ProjectToPermissionsUser(Guid userId, bool read) =>
        p => new ProjectPermissionDetails
        {
            Project = Mapper.Map<Bit.Core.SecretsManager.Entities.Project>(p),
            Read = read,
            Write = p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
                    p.GroupAccessPolicies.Any(ap =>
                        ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write)),
        };

    private Expression<Func<Project, ProjectPermissionDetails>> ProjectToPermissionsServiceAccount(Guid userId, bool read) =>
        p => new ProjectPermissionDetails
        {
            Project = Mapper.Map<Bit.Core.SecretsManager.Entities.Project>(p),
            Read = read,
            Write = p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccount.Id == userId && ap.Write),
        };

    private static Expression<Func<Project, bool>> UserHasReadAccessToProject(Guid userId) => p =>
        p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Read) ||
        p.GroupAccessPolicies.Any(ap => ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Read));

    private static Expression<Func<Project, bool>> UserHasWriteAccessToProject(Guid userId) => p =>
        p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
        p.GroupAccessPolicies.Any(ap => ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write));

    private static Expression<Func<Project, bool>> ServiceAccountHasReadAccessToProject(Guid serviceAccountId) => p =>
        p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccount.Id == serviceAccountId && ap.Read);
}

using System.Linq.Expressions;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Infrastructure.EntityFramework;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.SecretsManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.SecretsManager.Repositories;

public class SecretRepository : Repository<Core.SecretsManager.Entities.Secret, Secret, Guid>, ISecretRepository
{
    public SecretRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, db => db.Secret)
    { }

    public override async Task<Core.SecretsManager.Entities.Secret> GetByIdAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secret = await dbContext.Secret
                                    .Include("Projects")
                                    .Where(c => c.Id == id && c.DeletedDate == null)
                                    .FirstOrDefaultAsync();
            return Mapper.Map<Core.SecretsManager.Entities.Secret>(secret);
        }
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.Secret>> GetManyByIds(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secrets = await dbContext.Secret
                .Where(c => ids.Contains(c.Id) && c.DeletedDate == null)
                .Include(c => c.Projects)
                .ToListAsync();
            return Mapper.Map<List<Core.SecretsManager.Entities.Secret>>(secrets);
        }
    }

    public async Task<IEnumerable<SecretPermissionDetails>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Secret
            .Include(c => c.Projects)
            .Where(c => c.OrganizationId == organizationId && c.DeletedDate == null)
            .OrderBy(s => s.RevisionDate);

        var secrets = SecretToPermissionDetails(query, userId, accessType);

        return await secrets.ToListAsync();
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.Secret>> GetManyByOrganizationIdInTrashByIdsAsync(Guid organizationId, IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secrets = await dbContext.Secret
                                    .Where(s => ids.Contains(s.Id) && s.OrganizationId == organizationId && s.DeletedDate != null)
                                    .Include("Projects")
                                    .OrderBy(c => c.RevisionDate)
                                    .ToListAsync();

            return Mapper.Map<List<Core.SecretsManager.Entities.Secret>>(secrets);
        }
    }

    public async Task<IEnumerable<SecretPermissionDetails>> GetManyByOrganizationIdInTrashAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secrets = await dbContext.Secret
                                    .Where(c => c.OrganizationId == organizationId && c.DeletedDate != null)
                                    .Include("Projects")
                                    .OrderBy(c => c.RevisionDate)
                                    .ToListAsync();

            // This should be changed if/when we allow non admins to access trashed items
            return Mapper.Map<List<Core.SecretsManager.Entities.Secret>>(secrets).Select(s => new SecretPermissionDetails
            {
                Secret = s,
                Read = true,
                Write = true,
            });
        }
    }

    public async Task<IEnumerable<SecretPermissionDetails>> GetManyByProjectIdAsync(Guid projectId, Guid userId, AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Secret.Include(s => s.Projects)
            .Where(s => s.Projects.Any(p => p.Id == projectId) && s.DeletedDate == null);

        var secrets = SecretToPermissionDetails(query, userId, accessType);

        return await secrets.ToListAsync();
    }

    public override async Task<Core.SecretsManager.Entities.Secret> CreateAsync(Core.SecretsManager.Entities.Secret secret)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            secret.SetNewId();
            var entity = Mapper.Map<Secret>(secret);

            if (secret.Projects?.Count > 0)
            {
                foreach (var p in entity.Projects)
                {
                    dbContext.Attach(p);
                }
            }

            await dbContext.AddAsync(entity);
            await dbContext.SaveChangesAsync();
            secret.Id = entity.Id;
            return secret;
        }
    }

    public async Task<Core.SecretsManager.Entities.Secret> UpdateAsync(Core.SecretsManager.Entities.Secret secret)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var mappedEntity = Mapper.Map<Secret>(secret);

            var entity = await dbContext.Secret
                .Include("Projects")
                .FirstAsync(s => s.Id == secret.Id);

            foreach (var p in entity.Projects?.Where(p => mappedEntity.Projects.All(mp => mp.Id != p.Id)))
            {
                entity.Projects.Remove(p);
            }

            // Add new relationships
            foreach (var project in mappedEntity.Projects?.Where(p => entity.Projects.All(ep => ep.Id != p.Id)))
            {
                var p = dbContext.AttachToOrGet<Project>(_ => _.Id == project.Id, () => project);
                entity.Projects.Add(p);
            }

            dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);
            await dbContext.SaveChangesAsync();
        }

        return secret;
    }

    public async Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var utcNow = DateTime.UtcNow;
            var secrets = dbContext.Secret.Where(c => ids.Contains(c.Id));
            await secrets.ForEachAsync(secret =>
            {
                dbContext.Attach(secret);
                secret.DeletedDate = utcNow;
                secret.RevisionDate = utcNow;
            });
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task HardDeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secrets = dbContext.Secret.Where(c => ids.Contains(c.Id));
            await secrets.ForEachAsync(secret =>
            {
                dbContext.Attach(secret);
                dbContext.Remove(secret);
            });
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task RestoreManyByIdAsync(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var utcNow = DateTime.UtcNow;
            var secrets = dbContext.Secret.Where(c => ids.Contains(c.Id));
            await secrets.ForEachAsync(secret =>
            {
                dbContext.Attach(secret);
                secret.DeletedDate = null;
                secret.RevisionDate = utcNow;
            });
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.Secret>> ImportAsync(IEnumerable<Core.SecretsManager.Entities.Secret> secrets)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entities = new List<Secret>();
            var projects = secrets
                .SelectMany(s => s.Projects ?? Enumerable.Empty<Core.SecretsManager.Entities.Project>())
                .DistinctBy(p => p.Id)
                .Select(p => Mapper.Map<Project>(p))
                .ToDictionary(p => p.Id, p => p);

            dbContext.AttachRange(projects.Values);

            foreach (var s in secrets)
            {
                var entity = Mapper.Map<Secret>(s);

                if (s.Projects?.Count > 0)
                {
                    entity.Projects = s.Projects.Select(p => projects[p.Id]).ToList();
                }

                entities.Add(entity);
            }
            await GetDbSet(dbContext).AddRangeAsync(entities);
            await dbContext.SaveChangesAsync();
        }
        return secrets;
    }

    public async Task UpdateRevisionDates(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var utcNow = DateTime.UtcNow;
            var secrets = dbContext.Secret.Where(s => ids.Contains(s.Id));

            await secrets.ForEachAsync(secret =>
            {
                dbContext.Attach(secret);
                secret.RevisionDate = utcNow;
            });

            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<(bool Read, bool Write)> AccessToSecretAsync(Guid id, Guid userId, AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var secret = dbContext.Secret
            .Where(s => s.Id == id);

        var query = accessType switch
        {
            AccessClientType.NoAccessCheck => secret.Select(_ => new { Read = true, Write = true }),
            AccessClientType.User => secret.Select(s => new
            {
                Read = s.Projects.Any(p =>
                    p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Read) ||
                    p.GroupAccessPolicies.Any(ap =>
                        ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Read))),
                Write = s.Projects.Any(p =>
                    p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
                    p.GroupAccessPolicies.Any(ap =>
                        ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write))),
            }),
            AccessClientType.ServiceAccount => secret.Select(s => new
            {
                Read = s.Projects.Any(p =>
                    p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccountId == userId && ap.Read)),
                Write = s.Projects.Any(p =>
                    p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccountId == userId && ap.Write)),
            }),
            _ => secret.Select(_ => new { Read = false, Write = false }),
        };

        var policy = await query.FirstOrDefaultAsync();

        return (policy.Read, policy.Write);
    }

    public async Task EmptyTrash(DateTime nowTime, uint DeleteAfterThisNumberOfDays)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secrets = dbContext.Secret.Where(s => s.DeletedDate != null && (s.DeletedDate - nowTime) > TimeSpan.FromDays(DeleteAfterThisNumberOfDays));
            await secrets.ForEachAsync(secret =>
            {
                dbContext.Attach(secret);
                dbContext.Remove(secret);
            });
            await dbContext.SaveChangesAsync();
        }
    }

    private IQueryable<SecretPermissionDetails> SecretToPermissionDetails(IQueryable<Secret> query, Guid userId, AccessClientType accessType)
    {
        var secrets = accessType switch
        {
            AccessClientType.NoAccessCheck => query.Select(s => new SecretPermissionDetails
            {
                Secret = Mapper.Map<Bit.Core.SecretsManager.Entities.Secret>(s),
                Read = true,
                Write = true,
            }),
            AccessClientType.User => query.Where(UserHasReadAccessToSecret(userId)).Select(SecretToPermissionsUser(userId, true)),
            AccessClientType.ServiceAccount => query.Where(ServiceAccountHasReadAccessToSecret(userId)).Select(s =>
                new SecretPermissionDetails
                {
                    Secret = Mapper.Map<Bit.Core.SecretsManager.Entities.Secret>(s),
                    Read = true,
                    Write = false,
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };
        return secrets;
    }

    private Expression<Func<Secret, SecretPermissionDetails>> SecretToPermissionsUser(Guid userId, bool read) =>
        s => new SecretPermissionDetails
        {
            Secret = Mapper.Map<Bit.Core.SecretsManager.Entities.Secret>(s),
            Read = read,
            Write = s.Projects.Any(p =>
                p.UserAccessPolicies.Any(ap => ap.OrganizationUser.User.Id == userId && ap.Write) ||
                p.GroupAccessPolicies.Any(ap =>
                    ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.User.Id == userId && ap.Write))),
        };

    private static Expression<Func<Secret, bool>> ServiceAccountHasReadAccessToSecret(Guid serviceAccountId) => s =>
        s.Projects.Any(p =>
            p.ServiceAccountAccessPolicies.Any(ap => ap.ServiceAccount.Id == serviceAccountId && ap.Read));

    private static Expression<Func<Secret, bool>> UserHasReadAccessToSecret(Guid userId) => s =>
        s.Projects.Any(p =>
            p.UserAccessPolicies.Any(ap => ap.OrganizationUser.UserId == userId && ap.Read) ||
            p.GroupAccessPolicies.Any(ap =>
                ap.Group.GroupUsers.Any(gu => gu.OrganizationUser.UserId == userId && ap.Read)));
}

using System.Linq.Expressions;
using AutoMapper;
using Bit.Core.Enums;
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

    private static Expression<Func<Secret, bool>> UserHasReadAccessToSecret(Guid userId) => s =>
        s.Projects.Any(p =>
            p.UserAccessPolicies.Any(ap => ap.OrganizationUserId == userId && ap.Read) ||
            p.GroupAccessPolicies.Any(ap =>
                ap.Group.GroupUsers.Any(gu => gu.OrganizationUserId == userId && ap.Read)));


    public async Task<IEnumerable<Core.SecretsManager.Entities.Secret>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var query = dbContext.Secret.Where(c => c.OrganizationId == organizationId && c.DeletedDate == null);

        query = accessType switch
        {
            AccessClientType.NoAccessCheck => query,
            AccessClientType.User => query.Where(UserHasReadAccessToSecret(userId)),
            _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
        };

        var secrets = await query.Include(c => c.Projects).OrderBy(c => c.RevisionDate).ToListAsync();
        return Mapper.Map<List<Core.SecretsManager.Entities.Secret>>(secrets);
    }

    public async Task<IEnumerable<Core.SecretsManager.Entities.Secret>> GetManyByProjectIdAsync(Guid projectId, Guid userId, AccessClientType accessType)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.Secret.Include(s => s.Projects)
                .Where(s => s.Projects.Any(p => p.Id == projectId) && s.DeletedDate == null);

            query = accessType switch
            {
                AccessClientType.NoAccessCheck => query,
                AccessClientType.User => query.Where(UserHasReadAccessToSecret(userId)),
                _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
            };

            var secrets = await query.OrderBy(s => s.RevisionDate).ToListAsync();
            return Mapper.Map<List<Core.SecretsManager.Entities.Secret>>(secrets);
        }
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
}

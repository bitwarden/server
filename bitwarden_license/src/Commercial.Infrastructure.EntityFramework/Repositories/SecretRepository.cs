using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.Repositories;

public class SecretRepository : Repository<Core.Entities.Secret, Secret, Guid>, ISecretRepository
{
    public SecretRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, db => db.Secret)
    { }

    public override async Task<Core.Entities.Secret> GetByIdAsync(Guid id, Guid userId, AccessClientType accessType, bool orgAdmin)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.Secret
                                .Include("Projects")
                                .Where(c => c.Id == id && c.DeletedDate == null)
                                .FirstOrDefaultAsync();
            //TODO if projid is null then use orgAdmin check instead
            query = accessType switch
            {
                AccessClientType.NoAccessCheck => query,
                AccessClientType.User => query.Where(sec => _projectRepository.UserHasReadAccessToProject(sec.projectId, userId)),
                AccessClientType.ServiceAccount => query.Where(sec => _projectRepository.ServiceAccountHasReadAccessToProject(sec.projectId, userId)), 
                _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
            };

            var secret = await query;
            return Mapper.Map<Core.Entities.Secret>(secret);
        }
    }

    public async Task<IEnumerable<Core.Entities.Secret>> GetManyByIds(IEnumerable<Guid> ids, Guid userId, AccessClientType accessType, bool orgAdmin)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var query = dbContext.Secret
                                .Where(c => ids.Contains(c.Id) && c.DeletedDate == null)
                                .ToListAsync();

            //TODO if projid is null then use orgAdmin check instead
            query = accessType switch
            {
                AccessClientType.NoAccessCheck => query,
                AccessClientType.User => query.Where(sec => _projectRepository.UserHasReadAccessToProject(sec.projectId, userId)),
                AccessClientType.ServiceAccount => query.Where(sec => _projectRepository.ServiceAccountHasReadAccessToProject(sec.projectId, userId)), 
                _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
            };

            var secrets = await query;

            return Mapper.Map<List<Core.Entities.Secret>>(secrets);
        }
    }

    public async Task<IEnumerable<Core.Entities.Secret>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType, bool orgAdmin)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var query = dbContext.Secret
                        .Where(c => c.OrganizationId == organizationId && c.DeletedDate == null)
                        .Include("Projects")
                        .OrderBy(c => c.RevisionDate)
                        .ToListAsync();

            //Todo we need to see if projId is null, if so, use orgAdmin status if they can read/write
            //(!sec.projectId && orgAdmin) || hasAccess
            query = accessType switch
                AccessClientType.NoAccessCheck => query,
                AccessClientType.User => query.Where(sec => _projectRepository.UserHasReadAccessToProject(sec.projectId, userId)),
                AccessClientType.ServiceAccount => query.Where(sec => (_projectRepository.ServiceAccountHasReadAccessToProject(sec.projectId, userId)), 
                _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
            };

            var secrets = await query;
            return Mapper.Map<List<Core.Entities.Secret>>(secrets);
        }
    }

    public async Task<IEnumerable<Core.Entities.Secret>> GetManyByProjectIdAsync(Guid projectId, Guid userId, AccessClientType accessType, bool orgAdmin)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = dbContext.Secret
                .Where(s => s.Projects.Any(p => p.Id == projectId) && s.DeletedDate == null).Include("Projects")
                .OrderBy(s => s.RevisionDate).ToListAsync();

            query = accessType switch
            {
                //TODO move 84 down here if it works
                AccessClientType.NoAccessCheck => query,
                AccessClientType.User => query.Where(sec => _projectRepository.UserHasReadAccessToProject(sec.projectId, userId)),
                AccessClientType.ServiceAccount => query.Where(sec => _projectRepository.ServiceAccountHasReadAccessToProject(sec.projectId, userId)), 
                _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
            };

            var secrets = await query;

            return Mapper.Map<List<Core.Entities.Secret>>(secrets);
        }
    }

    public override async Task<Core.Entities.Secret> CreateAsync(Core.Entities.Secret secret, Guid userId, AccessClientType accessType, bool orgAdmin)
    {   
        var hasAccess = false;
        if(secret.projectId){
            hasAccess = accessType switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => _projectRepository.UserHasWriteAccessToProject(secret.projectId, userId),
                AccessClientType.ServiceAccount => _projectRepository.ServiceAccountHasWriteAccessToProject(secret.projectId, userId), 
                _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
            };
        } else {
            hasAccess = orgAdmin;
        }
        
        if(!hasAccess){
            throw UnauthorizedAccessException();
        }

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

    public async Task<Core.Entities.Secret> UpdateAsync(Core.Entities.Secret secret, Guid userId, AccessClientType accessType, bool orgAdmin)
    {
        var hasAccess = false;
        if(secret.projectId){
            hasAccess = accessType switch
            {
                AccessClientType.NoAccessCheck => true,
                AccessClientType.User => _projectRepository.UserHasWriteAccessToProject(secret.projectId, userId),
                AccessClientType.ServiceAccount => _projectRepository.ServiceAccountHasWriteAccessToProject(secret.projectId, userId), 
                _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
            };
        } else {
            hasAccess = orgAdmin;
        }
        
        if(!hasAccess){
            throw UnauthorizedAccessException();
        }

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

    public async Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids, Guid userId, AccessClientType accessType, bool orgAdmin)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var utcNow = DateTime.UtcNow;
            var secrets = dbContext.Secret.Where(c => ids.Contains(c.Id));
            
            await secrets.ForEachAsync(secret =>
            {
                //Todo change?
                var hasAccess = false;

                if(secret.projectId){
                    hasAccess = accessType switch
                    {
                        AccessClientType.NoAccessCheck => true,
                        AccessClientType.User => _projectRepository.UserHasWriteAccessToProject(secret.projectId, userId),
                        AccessClientType.ServiceAccount => _projectRepository.ServiceAccountHasWriteAccessToProject(secret.projectId, userId), 
                        _ => throw new ArgumentOutOfRangeException(nameof(accessType), accessType, null),
                    };
                } else {
                    hasAccess = orgAdmin;
                }
                
                if(!hasAccess){
                    throw UnauthorizedAccessException();
                }

                dbContext.Attach(secret);
                secret.DeletedDate = utcNow;
                secret.RevisionDate = utcNow;
            });
            await dbContext.SaveChangesAsync();
        }
    }
}

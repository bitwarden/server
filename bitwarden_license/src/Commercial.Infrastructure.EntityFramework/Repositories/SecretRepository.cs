using AutoMapper;
using Bit.Core.Repositories;
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

    public override async Task<Core.Entities.Secret> GetByIdAsync(Guid id)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secret = await dbContext.Secret
                                    .Include("Projects")
                                    .Where(c => c.Id == id && c.DeletedDate == null)
                                    .FirstOrDefaultAsync();
            return Mapper.Map<Core.Entities.Secret>(secret);
        }
    }

    public async Task<IEnumerable<Core.Entities.Secret>> GetManyByIds(IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secrets = await dbContext.Secret
                                    .Where(c => ids.Contains(c.Id) && c.DeletedDate == null)
                                    .ToListAsync();
            return Mapper.Map<List<Core.Entities.Secret>>(secrets);
        }
    }

    public async Task<IEnumerable<Core.Entities.Secret>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var secrets = await dbContext.Secret
                                    .Where(c => c.OrganizationId == organizationId && c.DeletedDate == null)
                                    .Include("Projects")
                                    .OrderBy(c => c.RevisionDate)
                                    .ToListAsync();

            return Mapper.Map<List<Core.Entities.Secret>>(secrets);
        }
    }

    public async Task<Core.Entities.Secret> UpdateAsync(Core.Entities.Secret secret, Guid[]? projectIds)
    {
        if (projectIds != null)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var mappedEntity = Mapper.Map<Secret>(secret);
                var entity = await GetDbSet(dbContext).FindAsync(secret.Id);


                foreach (var p in mappedEntity.Projects)
                {
                   //Do I need to load the project?
                    dbContext.Attach(p);
                }
                
                //TODO we need to remove the projects that aren't in the mappedEntity.Projects list
                //Then we need to add the one's that aren't in there yet --  If this isn't handled already automatically

                dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);
                await dbContext.SaveChangesAsync();
            }
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

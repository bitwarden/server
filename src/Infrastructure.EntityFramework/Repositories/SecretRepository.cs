using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories
{
    public class SecretRepository : Repository<Core.Entities.Secret, Secret, Guid>, ISecretRepository
    {
        public SecretRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, db => db.Secret)
        {

        }

        public override async Task ReplaceAsync(Core.Entities.Secret obj)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entity = await GetDbSet(dbContext).FindAsync(obj.Id);
                if (entity != null)
                {
                    var mappedEntity = Mapper.Map<Core.Entities.Secret>(obj);
                    // Set creation date to original date for the replace operation.
                    mappedEntity.CreationDate = entity.CreationDate;
                    dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        public async Task<IEnumerable<Core.Entities.Secret>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var secrets = await dbContext.Secret
                    .Where(c => c.OrganizationId == organizationId)
                    .ToListAsync();
                return Mapper.Map<List<Core.Entities.Secret>>(secrets);
            }
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
}

using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.Repositories
{
    public class ProjectSecretsRepository : Repository<Core.Entities.ProjectSecrets, ProjectSecrets, Guid>, IProjectSecretsRepository
    {
        public ProjectSecretsRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, db => db.ProjectSecrets)
        {

        }

        public override async Task<Core.Entities.ProjectSecrets> GetByIdAsync(Guid id)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var projectSecrets = await dbContext.ProjectSecrets
                                        .Where(c => c.Id == id && c.DeletedDate == null)
                                        .FirstOrDefaultAsync();
                return Mapper.Map<Core.Entities.ProjectSecrets>(projectSecrets);
            }
        }

        public async Task<IEnumerable<Core.Entities.ProjectSecrets>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var projectSecrets = await dbContext.ProjectSecrets
                                        .Where(c => c.OrganizationId == organizationId && c.DeletedDate == null)
                                        .OrderBy(c => c.RevisionDate)
                                        .ToListAsync();
                return Mapper.Map<List<Core.Entities.ProjectSecrets>>(projectSecrets);
            }
        }

        public async Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids)
        {
            //TODO soft delete the association, if it belongs to only one proj then we need to move it to the unassigned project 
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var utcNow = DateTime.UtcNow;
                var projectSecrets = dbContext.ProjectSecrets.Where(c => ids.Contains(c.Id));
                await projectSecrets.ForEachAsync(projectSecrets =>
                {
                    dbContext.Attach(projectSecrets);
                    projectSecrets.DeletedDate = utcNow;
                    projectSecrets.RevisionDate = utcNow;
                });
                await dbContext.SaveChangesAsync();
            }
        }
    }
}

using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.Repositories
{
    public class ProjectRepository : Repository<Core.Entities.Project, Project, Guid>, IProjectRepository
    {
        public ProjectRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, db => db.Project)
        {

        }

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

        public async Task<IEnumerable<Core.Entities.Project>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var project = await dbContext.Project
                                        .Where(c => c.OrganizationId == organizationId && c.DeletedDate == null)
                                        .OrderBy(c => c.RevisionDate)
                                        .ToListAsync();
                return Mapper.Map<List<Core.Entities.Project>>(project);
            }
        }

        public async Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var utcNow = DateTime.UtcNow;
                var project = dbContext.Project.Where(c => ids.Contains(c.Id));
                await project.ForEachAsync(project =>
                {
                    dbContext.Attach(project);
                    project.DeletedDate = utcNow;
                    project.RevisionDate = utcNow;
                });
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task AddSecretToProject(Guid projectId , Guid secretId){   
              using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                
                var project = await dbContext.Project
                                        .Where(c => c.Id == projectId && c.DeletedDate == null)
                                        .FirstAsync();

                
                var secret = await dbContext.Secret
                                        .Where(c => c.Id == secretId && c.DeletedDate == null)
                                        .FirstAsync();
                project.Secrets.Add(secret);
                await dbContext.SaveChangesAsync();
            }
        }

        // Get projects by secret SecretRepository

        // Add secret to project ProjectRepository

        // Add project to Secret SecretRepository 

        // Remove secrets from a project ProjectRepository

        // Remove project from a secret SecretRepository

        //get project with the id
        //get secret 
        //project.secrets.Add(secret)
    }
}

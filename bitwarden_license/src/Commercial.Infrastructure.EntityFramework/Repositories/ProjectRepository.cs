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
            : base(serviceScopeFactory, mapper, db => db.Projects)
        {

        }

        public override async Task<Core.Entities.Project> GetByIdAsync(Guid id)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var project = await dbContext.Projects
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
                var projects = await dbContext.Projects
                                        .Where(c => c.OrganizationId == organizationId && c.DeletedDate == null)
                                        .OrderBy(c => c.RevisionDate)
                                        .ToListAsync();
                return Mapper.Map<List<Core.Entities.Project>>(projects);
            }
        }

        public async Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var utcNow = DateTime.UtcNow;
                var projects = dbContext.Projects.Where(c => ids.Contains(c.Id));
                await projects.ForEachAsync(project =>
                {
                    dbContext.Attach(project);
                    project.DeletedDate = utcNow;
                    project.RevisionDate = utcNow;
                });
                await dbContext.SaveChangesAsync();
            }
        }
    }
}

using AutoMapper;
using Bit.Core.Dirt.Repositories;
using Bit.Infrastructure.EntityFramework.Dirt.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories;

public class PasswordHealthReportApplicationRepository :
    Repository<Core.Dirt.Entities.PasswordHealthReportApplication, PasswordHealthReportApplication, Guid>,
    IPasswordHealthReportApplicationRepository
{
    public PasswordHealthReportApplicationRepository(IServiceScopeFactory serviceScopeFactory,
        IMapper mapper) : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.PasswordHealthReportApplications)
    { }

    public async Task<ICollection<Core.Dirt.Entities.PasswordHealthReportApplication>> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.PasswordHealthReportApplications
                .Where(p => p.OrganizationId == organizationId)
                .ToListAsync();
            return Mapper.Map<ICollection<Core.Dirt.Entities.PasswordHealthReportApplication>>(results);
        }
    }
}

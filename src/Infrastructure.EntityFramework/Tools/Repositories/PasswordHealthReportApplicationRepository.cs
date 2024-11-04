using AutoMapper;
using Bit.Core.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using AdminConsoleEntities = Bit.Core.Tools.Entities;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class PasswordHealthReportApplicationRepository :
    Repository<AdminConsoleEntities.PasswordHealthReportApplication, Models.PasswordHealthReportApplication, Guid>,
    IPasswordHealthReportApplicationRepository
{
    public PasswordHealthReportApplicationRepository(IServiceScopeFactory serviceScopeFactory,
        IMapper mapper) : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.PasswordHealthReportApplications)
    { }

    public async Task<ICollection<AdminConsoleEntities.PasswordHealthReportApplication>> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.PasswordHealthReportApplications
                .Where(p => p.OrganizationId == organizationId)
                .ToListAsync();
            return Mapper.Map<ICollection<AdminConsoleEntities.PasswordHealthReportApplication>>(results);
        }
    }
}

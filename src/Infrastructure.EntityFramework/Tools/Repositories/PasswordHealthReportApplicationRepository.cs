using AutoMapper;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Tools.Models;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using AdminConsoleEntities = Bit.Core.Tools.Entities;

namespace Bit.Infrastructure.EntityFramework.Tools.Repositories;

public class PasswordHealthReportApplicationRepository :
    Repository<AdminConsoleEntities.PasswordHealthReportApplication, PasswordHealthReportApplication, Guid>,
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

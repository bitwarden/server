using AutoMapper;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AdminConsoleEntities = Bit.Core.AdminConsole.Entities;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class OrganizationPartnershipReconciliationExportRepository
    : Repository<AdminConsoleEntities.OrganizationPartnershipReconciliationExport,
        OrganizationPartnershipReconciliationExport, Guid>,
      IOrganizationPartnershipReconciliationExportRepository
{
    public OrganizationPartnershipReconciliationExportRepository(
        IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.OrganizationPartnershipReconciliationExports)
    {
    }

    public async Task<ICollection<AdminConsoleEntities.OrganizationPartnershipReconciliationExport>>
        GetManyPendingAsync()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var results = await dbContext.OrganizationPartnershipReconciliationExports
            .Where(e => e.Status == PartnershipReconciliationExportStatus.Pending)
            .OrderBy(e => e.CreationDate)
            .ToListAsync();
        return Mapper.Map<List<AdminConsoleEntities.OrganizationPartnershipReconciliationExport>>(results);
    }

    public async Task<ICollection<AdminConsoleEntities.OrganizationPartnershipReconciliationExport>>
        GetManyExpiredAsync(DateTime asOf)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var results = await dbContext.OrganizationPartnershipReconciliationExports
            .Where(e => e.ExpirationDate <= asOf)
            .ToListAsync();
        return Mapper.Map<List<AdminConsoleEntities.OrganizationPartnershipReconciliationExport>>(results);
    }

    public async Task<bool> StartIfPendingAsync(Guid id, DateTime revisionDate)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var rowsAffected = await dbContext.OrganizationPartnershipReconciliationExports
            .Where(e => e.Id == id && e.Status == PartnershipReconciliationExportStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, PartnershipReconciliationExportStatus.Running)
                .SetProperty(e => e.RevisionDate, revisionDate));
        return rowsAffected > 0;
    }
}

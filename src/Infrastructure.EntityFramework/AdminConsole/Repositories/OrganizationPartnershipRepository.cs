using AutoMapper;
using Bit.Core.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AdminConsoleEntities = Bit.Core.AdminConsole.Entities;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class OrganizationPartnershipRepository
    : Repository<AdminConsoleEntities.OrganizationPartnership, OrganizationPartnership, Guid>,
      IOrganizationPartnershipRepository
{
    public OrganizationPartnershipRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.OrganizationPartnerships)
    {
    }

    public async Task<AdminConsoleEntities.OrganizationPartnership?> GetByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var result = await dbContext.OrganizationPartnerships
            .FirstOrDefaultAsync(e => e.OrganizationId == organizationId);
        return Mapper.Map<AdminConsoleEntities.OrganizationPartnership?>(result);
    }

    public override async Task DeleteAsync(AdminConsoleEntities.OrganizationPartnership partnership)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        await dbContext.OrganizationPartnershipEntitlements
            .Where(e => e.OrganizationPartnershipId == partnership.Id)
            .ExecuteDeleteAsync();
        await dbContext.OrganizationPartnerships
            .Where(p => p.Id == partnership.Id)
            .ExecuteDeleteAsync();

        await transaction.CommitAsync();
    }
}

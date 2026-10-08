using AutoMapper;
using Bit.Core;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AdminConsoleEntities = Bit.Core.AdminConsole.Entities;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class OrganizationPartnershipEntitlementRepository
    : Repository<AdminConsoleEntities.OrganizationPartnershipEntitlement, OrganizationPartnershipEntitlement, Guid>,
      IOrganizationPartnershipEntitlementRepository
{
    private readonly IDataProtector _dataProtector;

    public OrganizationPartnershipEntitlementRepository(
        IServiceScopeFactory serviceScopeFactory, IMapper mapper,
        IDataProtectionProvider dataProtectionProvider)
        : base(serviceScopeFactory, mapper, context => context.OrganizationPartnershipEntitlements)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
    }

    public override async Task<AdminConsoleEntities.OrganizationPartnershipEntitlement?> GetByIdAsync(Guid id)
    {
        var entitlement = await base.GetByIdAsync(id);
        UnprotectData(entitlement);
        return entitlement;
    }

    public async Task<AdminConsoleEntities.OrganizationPartnershipEntitlement?> GetByExternalIdAsync(
        Guid organizationPartnershipId, string externalId)
    {
        var externalIdHash = AdminConsoleEntities.OrganizationPartnershipEntitlement
            .ComputeExternalIdHash(organizationPartnershipId, externalId);

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var result = await dbContext.OrganizationPartnershipEntitlements
            .FirstOrDefaultAsync(e =>
                e.OrganizationPartnershipId == organizationPartnershipId &&
                e.ExternalIdHash == externalIdHash);
        var entitlement = Mapper.Map<AdminConsoleEntities.OrganizationPartnershipEntitlement?>(result);
        UnprotectData(entitlement);
        return entitlement;
    }

    public async Task<ICollection<AdminConsoleEntities.OrganizationPartnershipEntitlement>>
        GetManyCanceledWithExpiredResumeWindowAsync(DateTime asOf)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var results = await dbContext.OrganizationPartnershipEntitlements
            .Where(e =>
                e.State == PartnershipEntitlementState.Canceled &&
                e.UserId != null &&
                e.ResumeWindowExpirationDate <= asOf)
            .ToListAsync();
        var entitlements = Mapper.Map<List<AdminConsoleEntities.OrganizationPartnershipEntitlement>>(results);
        entitlements.ForEach(UnprotectData);
        return entitlements;
    }

    public override async Task<AdminConsoleEntities.OrganizationPartnershipEntitlement> CreateAsync(
        AdminConsoleEntities.OrganizationPartnershipEntitlement entitlement)
    {
        await ProtectDataAndSaveAsync(entitlement, () => base.CreateAsync(entitlement));
        return entitlement;
    }

    public override async Task ReplaceAsync(AdminConsoleEntities.OrganizationPartnershipEntitlement entitlement)
    {
        await ProtectDataAndSaveAsync(entitlement, () => base.ReplaceAsync(entitlement));
    }

    private async Task ProtectDataAndSaveAsync(
        AdminConsoleEntities.OrganizationPartnershipEntitlement entitlement, Func<Task> saveTask)
    {
        var originalExternalId = entitlement.ExternalId;
        entitlement.ExternalId = DatabaseFieldProtectionHelper.Protect(_dataProtector, entitlement.ExternalId)!;
        try
        {
            await saveTask();
        }
        finally
        {
            entitlement.ExternalId = originalExternalId;
        }
    }

    private void UnprotectData(AdminConsoleEntities.OrganizationPartnershipEntitlement? entitlement)
    {
        if (entitlement is not null)
        {
            entitlement.ExternalId = DatabaseFieldProtectionHelper.Unprotect(_dataProtector, entitlement.ExternalId)!;
        }
    }
}

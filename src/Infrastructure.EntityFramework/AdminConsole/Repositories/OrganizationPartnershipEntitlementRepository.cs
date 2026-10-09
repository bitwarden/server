using AutoMapper;
using Bit.Core;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Settings;
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
    private readonly GlobalSettings.PartnershipSettings _partnershipSettings;

    public OrganizationPartnershipEntitlementRepository(
        IServiceScopeFactory serviceScopeFactory, IMapper mapper,
        IDataProtectionProvider dataProtectionProvider, GlobalSettings globalSettings)
        : base(serviceScopeFactory, mapper, context => context.OrganizationPartnershipEntitlements)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
        _partnershipSettings = globalSettings.Partnerships;
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
        var externalIdHash = ComputeExternalIdHash(organizationPartnershipId, externalId);

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

    public async Task<bool> ReplaceIfUnchangedAsync(
        AdminConsoleEntities.OrganizationPartnershipEntitlement entitlement, DateTime expectedRevisionDate)
    {
        if (entitlement.RevisionDate == expectedRevisionDate)
        {
            throw new ArgumentException("RevisionDate must be advanced before a conditional replace.", nameof(entitlement));
        }

        var rowsAffected = 0;
        await ProtectDataAndSaveAsync(entitlement, async () =>
        {
            using var scope = ServiceScopeFactory.CreateScope();
            var dbContext = GetDatabaseContext(scope);
            rowsAffected = await dbContext.OrganizationPartnershipEntitlements
                .Where(e => e.Id == entitlement.Id && e.RevisionDate == expectedRevisionDate)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.ExternalId, entitlement.ExternalId)
                    .SetProperty(e => e.ExternalIdHash, entitlement.ExternalIdHash)
                    .SetProperty(e => e.State, entitlement.State)
                    .SetProperty(e => e.UserId, entitlement.UserId)
                    .SetProperty(e => e.AccountRef, entitlement.AccountRef)
                    .SetProperty(e => e.Metadata, entitlement.Metadata)
                    .SetProperty(e => e.BoundDate, entitlement.BoundDate)
                    .SetProperty(e => e.SuspendedDate, entitlement.SuspendedDate)
                    .SetProperty(e => e.CanceledDate, entitlement.CanceledDate)
                    .SetProperty(e => e.ResumeWindowExpirationDate, entitlement.ResumeWindowExpirationDate)
                    .SetProperty(e => e.LastAppliedEffectiveDate, entitlement.LastAppliedEffectiveDate)
                    .SetProperty(e => e.RevisionDate, entitlement.RevisionDate));
        });
        return rowsAffected > 0;
    }

    public async Task<bool> ReleaseExpiredResumeWindowBindingAsync(Guid id, DateTime asOf, DateTime revisionDate)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var rowsAffected = await dbContext.OrganizationPartnershipEntitlements
            .Where(e =>
                e.Id == id &&
                e.State == PartnershipEntitlementState.Canceled &&
                e.UserId != null &&
                e.ResumeWindowExpirationDate <= asOf)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.UserId, (Guid?)null)
                .SetProperty(e => e.AccountRef, (Guid?)null)
                .SetProperty(e => e.RevisionDate, revisionDate));
        return rowsAffected > 0;
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
        entitlement.ExternalIdHash = ComputeExternalIdHash(entitlement.OrganizationPartnershipId, originalExternalId);
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

    private string ComputeExternalIdHash(Guid organizationPartnershipId, string externalId) =>
        AdminConsoleEntities.OrganizationPartnershipEntitlement.ComputeExternalIdHash(
            _partnershipSettings.GetExternalIdHashKey(), organizationPartnershipId, externalId);

    private void UnprotectData(AdminConsoleEntities.OrganizationPartnershipEntitlement? entitlement)
    {
        if (entitlement is not null)
        {
            entitlement.ExternalId = DatabaseFieldProtectionHelper.Unprotect(_dataProtector, entitlement.ExternalId)!;
        }
    }
}

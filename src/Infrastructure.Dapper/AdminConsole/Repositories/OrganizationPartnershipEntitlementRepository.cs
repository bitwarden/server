using System.Data;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.AdminConsole.Repositories;

public class OrganizationPartnershipEntitlementRepository
    : Repository<OrganizationPartnershipEntitlement, Guid>, IOrganizationPartnershipEntitlementRepository
{
    private readonly IDataProtector _dataProtector;
    private readonly GlobalSettings.PartnershipSettings _partnershipSettings;

    public OrganizationPartnershipEntitlementRepository(
        GlobalSettings globalSettings,
        IDataProtectionProvider dataProtectionProvider)
        : this(globalSettings.SqlServer.ConnectionString,
               globalSettings.SqlServer.ReadOnlyConnectionString,
               dataProtectionProvider,
               globalSettings.Partnerships)
    { }

    public OrganizationPartnershipEntitlementRepository(
        string connectionString,
        string readOnlyConnectionString,
        IDataProtectionProvider dataProtectionProvider,
        GlobalSettings.PartnershipSettings partnershipSettings)
        : base(connectionString, readOnlyConnectionString)
    {
        _dataProtector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
        _partnershipSettings = partnershipSettings;
    }

    public override async Task<OrganizationPartnershipEntitlement?> GetByIdAsync(Guid id)
    {
        var entitlement = await base.GetByIdAsync(id);
        UnprotectData(entitlement);
        return entitlement;
    }

    public async Task<OrganizationPartnershipEntitlement?> GetByExternalIdAsync(
        Guid organizationPartnershipId, string externalId)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<OrganizationPartnershipEntitlement>(
            $"[{Schema}].[{Table}_ReadByOrganizationPartnershipIdExternalIdHash]",
            new
            {
                OrganizationPartnershipId = organizationPartnershipId,
                ExternalIdHash = ComputeExternalIdHash(organizationPartnershipId, externalId),
            },
            commandType: CommandType.StoredProcedure);
        var entitlement = results.SingleOrDefault();
        UnprotectData(entitlement);
        return entitlement;
    }

    public async Task<ICollection<OrganizationPartnershipEntitlement>> GetManyCanceledWithExpiredResumeWindowAsync(
        DateTime asOf)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<OrganizationPartnershipEntitlement>(
            $"[{Schema}].[{Table}_ReadManyCanceledWithExpiredResumeWindow]",
            new { AsOf = asOf },
            commandType: CommandType.StoredProcedure);
        var entitlements = results.ToList();
        foreach (var entitlement in entitlements)
        {
            UnprotectData(entitlement);
        }
        return entitlements;
    }

    public async Task<bool> ReleaseExpiredResumeWindowBindingAsync(Guid id, DateTime asOf, DateTime revisionDate)
    {
        using var connection = new SqlConnection(ConnectionString);
        var rowsAffected = await connection.ExecuteScalarAsync<int>(
            $"[{Schema}].[{Table}_ReleaseExpiredResumeWindowBinding]",
            new { Id = id, AsOf = asOf, RevisionDate = revisionDate },
            commandType: CommandType.StoredProcedure);
        return rowsAffected > 0;
    }

    public override async Task<OrganizationPartnershipEntitlement> CreateAsync(
        OrganizationPartnershipEntitlement entitlement)
    {
        await ProtectDataAndSaveAsync(entitlement, () => base.CreateAsync(entitlement));
        return entitlement;
    }

    public override async Task ReplaceAsync(OrganizationPartnershipEntitlement entitlement)
    {
        await ProtectDataAndSaveAsync(entitlement, () => base.ReplaceAsync(entitlement));
    }

    private async Task ProtectDataAndSaveAsync(OrganizationPartnershipEntitlement entitlement, Func<Task> saveTask)
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
        OrganizationPartnershipEntitlement.ComputeExternalIdHash(
            _partnershipSettings.GetExternalIdHashKey(), organizationPartnershipId, externalId);

    private void UnprotectData(OrganizationPartnershipEntitlement? entitlement)
    {
        if (entitlement == null)
        {
            return;
        }

        entitlement.ExternalId = DatabaseFieldProtectionHelper.Unprotect(_dataProtector, entitlement.ExternalId)!;
    }
}

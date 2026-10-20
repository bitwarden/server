using System.Data;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
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

    public async Task<ICollection<OrganizationPartnershipEntitlement>> GetPageByOrganizationPartnershipIdAsync(
        Guid organizationPartnershipId, Guid? afterId, int pageSize)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<OrganizationPartnershipEntitlement>(
            $"[{Schema}].[{Table}_ReadPageByOrganizationPartnershipId]",
            new { OrganizationPartnershipId = organizationPartnershipId, AfterId = afterId, PageSize = pageSize },
            commandType: CommandType.StoredProcedure);
        var entitlements = results.ToList();
        foreach (var entitlement in entitlements)
        {
            UnprotectData(entitlement);
        }
        return entitlements;
    }

    public async Task<ICollection<OrganizationPartnershipEntitlement>> GetPageByRevisionDateAsync(
        Guid organizationPartnershipId, PartnershipEntitlementState? state, DateTime? revisedAfter,
        PartnershipEntitlementKeyset? after, int pageSize)
    {
        var parameters = new DynamicParameters(new
        {
            OrganizationPartnershipId = organizationPartnershipId,
            State = (byte?)state,
            AfterId = after?.Id,
            PageSize = pageSize,
        });
        parameters.Add("RevisedAfter", revisedAfter, DbType.DateTime2, null, 7);
        parameters.Add("AfterRevisionDate", after?.RevisionDate, DbType.DateTime2, null, 7);
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<OrganizationPartnershipEntitlement>(
            $"[{Schema}].[{Table}_ReadPageByOrganizationPartnershipIdRevisionDate]",
            parameters,
            commandType: CommandType.StoredProcedure);
        var entitlements = results.ToList();
        foreach (var entitlement in entitlements)
        {
            UnprotectData(entitlement);
        }
        return entitlements;
    }

    public async Task<bool> ReplaceIfUnchangedAsync(
        OrganizationPartnershipEntitlement entitlement, DateTime expectedRevisionDate)
    {
        if (entitlement.RevisionDate <= expectedRevisionDate)
        {
            throw new ArgumentException("RevisionDate must be advanced before a conditional replace.", nameof(entitlement));
        }

        var rowsAffected = 0;
        await ProtectDataAndSaveAsync(entitlement, async () =>
        {
            // Dapper's default DateTime mapping rounds to 1/300 s, which could swallow a small revision step.
            var parameters = new DynamicParameters(entitlement);
            parameters.Add(nameof(entitlement.RevisionDate), entitlement.RevisionDate, DbType.DateTime2, null, 7);
            parameters.Add("ExpectedRevisionDate", expectedRevisionDate, DbType.DateTime2, null, 7);
            using var connection = new SqlConnection(ConnectionString);
            rowsAffected = await connection.ExecuteScalarAsync<int>(
                $"[{Schema}].[{Table}_UpdateIfUnchanged]",
                parameters,
                commandType: CommandType.StoredProcedure);
        });
        return rowsAffected > 0;
    }

    public async Task<bool> ReleaseExpiredResumeWindowBindingAsync(Guid id, DateTime asOf, DateTime revisionDate)
    {
        var parameters = new DynamicParameters(new { Id = id, AsOf = asOf });
        parameters.Add("RevisionDate", revisionDate, DbType.DateTime2, null, 7);
        using var connection = new SqlConnection(ConnectionString);
        var rowsAffected = await connection.ExecuteScalarAsync<int>(
            $"[{Schema}].[{Table}_ReleaseExpiredResumeWindowBinding]",
            parameters,
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

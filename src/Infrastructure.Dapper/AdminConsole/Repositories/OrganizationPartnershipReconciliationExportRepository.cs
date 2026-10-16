using System.Data;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.AdminConsole.Repositories;

public class OrganizationPartnershipReconciliationExportRepository
    : Repository<OrganizationPartnershipReconciliationExport, Guid>, IOrganizationPartnershipReconciliationExportRepository
{
    public OrganizationPartnershipReconciliationExportRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationPartnershipReconciliationExportRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<OrganizationPartnershipReconciliationExport>> GetManyPendingAsync()
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<OrganizationPartnershipReconciliationExport>(
            $"[{Schema}].[{Table}_ReadManyPending]",
            commandType: CommandType.StoredProcedure);
        return results.ToList();
    }

    public async Task<ICollection<OrganizationPartnershipReconciliationExport>> GetManyExpiredAsync(DateTime asOf)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<OrganizationPartnershipReconciliationExport>(
            $"[{Schema}].[{Table}_ReadManyExpired]",
            new { AsOf = asOf },
            commandType: CommandType.StoredProcedure);
        return results.ToList();
    }

    public async Task<bool> StartIfPendingAsync(Guid id, DateTime revisionDate)
    {
        using var connection = new SqlConnection(ConnectionString);
        var rowsAffected = await connection.ExecuteScalarAsync<int>(
            $"[{Schema}].[{Table}_StartIfPending]",
            new { Id = id, RevisionDate = revisionDate },
            commandType: CommandType.StoredProcedure);
        return rowsAffected > 0;
    }
}

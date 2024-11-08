using System.Data;
using Bit.Core.Settings;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;
using ToolsEntities = Bit.Core.Tools.Entities;

namespace Bit.Infrastructure.Dapper.Tools.Repositories;

public class PasswordHealthReportApplicationRepository : Repository<ToolsEntities.PasswordHealthReportApplication, Guid>, IPasswordHealthReportApplicationRepository
{
    public PasswordHealthReportApplicationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public PasswordHealthReportApplicationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<ToolsEntities.PasswordHealthReportApplication>> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<ToolsEntities.PasswordHealthReportApplication>(
                $"[{Schema}].[PasswordHealthReportApplication_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}

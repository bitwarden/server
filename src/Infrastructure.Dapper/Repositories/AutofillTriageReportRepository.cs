using System.Data;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Repositories;

public class AutofillTriageReportRepository(GlobalSettings globalSettings)
    : Repository<AutofillTriageReport, Guid>(
        globalSettings.SqlServer.ConnectionString,
        globalSettings.SqlServer.ReadOnlyConnectionString),
      IAutofillTriageReportRepository
{
    public async Task<IEnumerable<AutofillTriageReport>> GetActiveAsync(int skip, int take)
    {
        using var connection = new SqlConnection(ReadOnlyConnectionString);
        return await connection.QueryAsync<AutofillTriageReport>(
            "[dbo].[AutofillTriageReport_ReadActiveWithPagination]",
            new { Skip = skip, Take = take },
            commandType: CommandType.StoredProcedure);
    }

    public async Task ArchiveAsync(Guid id)
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            "[dbo].[AutofillTriageReport_Archive]",
            new { Id = id },
            commandType: CommandType.StoredProcedure);
    }
}

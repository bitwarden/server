// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Data;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt;

public class OrganizationReportRepository : Repository<OrganizationReport, Guid>, IOrganizationReportRepository
{
    public OrganizationReportRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public OrganizationReportRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    {
    }

    public async Task<OrganizationReport> GetLatestByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var result = await connection.QuerySingleOrDefaultAsync<OrganizationReport>(
                $"[{Schema}].[OrganizationReport_GetLatestByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return result;
        }
    }

    public async Task<OrganizationReport> UpdateSummaryDataAsync(Guid organizationId, Guid reportId, string summaryData)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var parameters = new
            {
                Id = reportId,
                OrganizationId = organizationId,
                SummaryData = summaryData,
                RevisionDate = DateTime.UtcNow
            };

            await connection.ExecuteAsync(
                $"[{Schema}].[OrganizationReport_UpdateSummaryData]",
                parameters,
                commandType: CommandType.StoredProcedure);

            // Return the updated report
            return await connection.QuerySingleOrDefaultAsync<OrganizationReport>(
                $"[{Schema}].[OrganizationReport_ReadById]",
                new { Id = reportId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<OrganizationReportSummaryDataResponse> GetSummaryDataAsync(Guid reportId)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var result = await connection.QuerySingleOrDefaultAsync<OrganizationReportSummaryDataResponse>(
                $"[{Schema}].[OrganizationReport_GetSummaryDataById]",
                new { Id = reportId },
                commandType: CommandType.StoredProcedure);

            return result;
        }
    }

    public async Task<IEnumerable<OrganizationReportSummaryDataResponse>> GetSummaryDataByDateRangeAsync(
        Guid organizationId,
        DateTime startDate, DateTime
            endDate)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var parameters = new
            {
                OrganizationId = organizationId,
                StartDate = startDate,
                EndDate = endDate
            };

            var results = await connection.QueryAsync<OrganizationReportSummaryDataResponse>(
                $"[{Schema}].[OrganizationReport_GetSummariesByDateRange]",
                parameters,
                commandType: CommandType.StoredProcedure);

            return results;
        }
    }

    public async Task<OrganizationReportDataResponse> GetReportDataAsync(Guid reportId)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var result = await connection.QuerySingleOrDefaultAsync<OrganizationReportDataResponse>(
                $"[{Schema}].[OrganizationReport_GetReportDataById]",
                new { Id = reportId },
                commandType: CommandType.StoredProcedure);

            return result;
        }
    }

    public async Task<OrganizationReport> UpdateReportDataAsync(Guid organizationId, Guid reportId, string reportData)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var parameters = new
            {
                OrganizationId = organizationId,
                Id = reportId,
                ReportData = reportData,
                RevisionDate = DateTime.UtcNow
            };

            await connection.ExecuteAsync(
                $"[{Schema}].[OrganizationReport_UpdateReportData]",
                parameters,
                commandType: CommandType.StoredProcedure);

            // Return the updated report
            return await connection.QuerySingleOrDefaultAsync<OrganizationReport>(
                $"[{Schema}].[OrganizationReport_ReadById]",
                new { Id = reportId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<OrganizationReportApplicationDataResponse> GetApplicationDataAsync(Guid reportId)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var result = await connection.QuerySingleOrDefaultAsync<OrganizationReportApplicationDataResponse>(
                $"[{Schema}].[OrganizationReport_GetApplicationDataById]",
                new { Id = reportId },
                commandType: CommandType.StoredProcedure);

            return result;
        }
    }

    public async Task<OrganizationReport> UpdateApplicationDataAsync(Guid organizationId, Guid reportId, string applicationData)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var parameters = new
            {
                OrganizationId = organizationId,
                Id = reportId,
                ApplicationData = applicationData,
                RevisionDate = DateTime.UtcNow
            };

            await connection.ExecuteAsync(
                $"[{Schema}].[OrganizationReport_UpdateApplicationData]",
                parameters,
                commandType: CommandType.StoredProcedure);

            // Return the updated report
            return await connection.QuerySingleOrDefaultAsync<OrganizationReport>(
                $"[{Schema}].[OrganizationReport_ReadById]",
                new { Id = reportId },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task UpdateMetricsAsync(Guid organizationId, Guid reportId, OrganizationReportMetricsData metrics)
    {
        using var connection = new SqlConnection(ConnectionString);
        var parameters = new
        {
            OrganizationId = organizationId,
            Id = reportId,
            ApplicationCount = metrics.ApplicationCount,
            ApplicationAtRiskCount = metrics.ApplicationAtRiskCount,
            CriticalApplicationCount = metrics.CriticalApplicationCount,
            CriticalApplicationAtRiskCount = metrics.CriticalApplicationAtRiskCount,
            MemberCount = metrics.MemberCount,
            MemberAtRiskCount = metrics.MemberAtRiskCount,
            CriticalMemberCount = metrics.CriticalMemberCount,
            CriticalMemberAtRiskCount = metrics.CriticalMemberAtRiskCount,
            PasswordCount = metrics.PasswordCount,
            PasswordAtRiskCount = metrics.PasswordAtRiskCount,
            CriticalPasswordCount = metrics.CriticalPasswordCount,
            CriticalPasswordAtRiskCount = metrics.CriticalPasswordAtRiskCount,
            RevisionDate = DateTime.UtcNow
        };

        await connection.ExecuteAsync(
            $"[{Schema}].[OrganizationReport_UpdateMetrics]",
            parameters,
            commandType: CommandType.StoredProcedure);
    }
}

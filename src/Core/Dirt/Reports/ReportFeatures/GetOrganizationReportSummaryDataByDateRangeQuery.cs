using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportSummaryDataByDateRangeQuery : IGetOrganizationReportSummaryDataByDateRangeQuery
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<GetOrganizationReportSummaryDataByDateRangeQuery> _logger;

    public GetOrganizationReportSummaryDataByDateRangeQuery(
        IOrganizationReportRepository organizationReportRepo,
        ILogger<GetOrganizationReportSummaryDataByDateRangeQuery> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _logger = logger;
    }

    public async Task<IEnumerable<OrganizationReportSummaryDataResponse>> GetOrganizationReportSummaryDataByDateRangeAsync(Guid organizationId, DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Fetching organization report summary data by date range for organization {organizationId}, from {startDate} to {endDate}",
                organizationId, startDate, endDate);

            var (isValid, errorMessage) = ValidateRequest(organizationId, startDate, endDate);
            if (!isValid)
            {
                _logger.LogWarning("GetOrganizationReportSummaryDataByDateRangeAsync validation failed: {errorMessage}", errorMessage);
                throw new BadRequestException(errorMessage);
            }

            IEnumerable<OrganizationReportSummaryDataResponse> summaryDataList = await _organizationReportRepo
                .GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate);

            if (summaryDataList == null)
            {
                _logger.LogInformation("No summary data found for organization {organizationId} in date range {startDate} to {endDate}",
                    organizationId, startDate, endDate);
                return Enumerable.Empty<OrganizationReportSummaryDataResponse>();
            }

            var resultList = summaryDataList.ToList();
            _logger.LogInformation("Successfully retrieved {count} organization report summary data records for organization {organizationId} in date range {startDate} to {endDate}",
                resultList.Count, organizationId, startDate, endDate);

            return resultList;
        }
        catch (Exception ex) when (!(ex is BadRequestException))
        {
            _logger.LogError(ex, "Error fetching organization report summary data by date range for organization {organizationId}, from {startDate} to {endDate}",
                organizationId, startDate, endDate);
            throw;
        }
    }

    private static (bool IsValid, string errorMessage) ValidateRequest(Guid organizationId, DateTime startDate, DateTime endDate)
    {
        if (organizationId == Guid.Empty)
        {
            return (false, "OrganizationId is required");
        }

        if (startDate == default)
        {
            return (false, "StartDate is required");
        }

        if (endDate == default)
        {
            return (false, "EndDate is required");
        }

        if (startDate > endDate)
        {
            return (false, "StartDate must be earlier than or equal to EndDate");
        }

        return (true, string.Empty);
    }
}

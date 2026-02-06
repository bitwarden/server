using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportSummaryDataQuery : IGetOrganizationReportSummaryDataQuery
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<GetOrganizationReportSummaryDataQuery> _logger;

    public GetOrganizationReportSummaryDataQuery(
        IOrganizationReportRepository organizationReportRepo,
        ILogger<GetOrganizationReportSummaryDataQuery> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _logger = logger;
    }

    public async Task<OrganizationReportSummaryDataResponse> GetOrganizationReportSummaryDataAsync(Guid organizationId, Guid reportId)
    {
        try
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, "Fetching organization report summary data for organization {organizationId} and report {reportId}",
                organizationId, reportId);

            if (organizationId == Guid.Empty)
            {
                _logger.LogWarning(Constants.BypassFiltersEventId, "GetOrganizationReportSummaryDataAsync called with empty OrganizationId");
                throw new BadRequestException("OrganizationId is required.");
            }

            if (reportId == Guid.Empty)
            {
                _logger.LogWarning(Constants.BypassFiltersEventId, "GetOrganizationReportSummaryDataAsync called with empty ReportId");
                throw new BadRequestException("ReportId is required.");
            }

            var summaryDataResponse = await _organizationReportRepo.GetSummaryDataAsync(reportId);

            if (summaryDataResponse == null)
            {
                _logger.LogWarning(Constants.BypassFiltersEventId, "No summary data found for organization {organizationId} and report {reportId}",
                    organizationId, reportId);
                throw new NotFoundException("Organization report summary data not found.");
            }

            _logger.LogInformation(Constants.BypassFiltersEventId, "Successfully retrieved organization report summary data for organization {organizationId} and report {reportId}",
                organizationId, reportId);

            return summaryDataResponse;
        }
        catch (Exception ex) when (!(ex is BadRequestException || ex is NotFoundException))
        {
            _logger.LogError(ex, "Error fetching organization report summary data for organization {organizationId} and report {reportId}",
                organizationId, reportId);
            throw;
        }
    }
}

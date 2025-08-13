using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportDataQuery : IGetOrganizationReportDataQuery
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<GetOrganizationReportDataQuery> _logger;

    public GetOrganizationReportDataQuery(
        IOrganizationReportRepository organizationReportRepo,
        ILogger<GetOrganizationReportDataQuery> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _logger = logger;
    }

    public async Task<OrganizationReportDataResponse> GetOrganizationReportDataAsync(Guid organizationId, Guid reportId)
    {
        try
        {
            _logger.LogInformation("Fetching organization report data for organization {organizationId} and report {reportId}",
                organizationId, reportId);

            if (organizationId == Guid.Empty)
            {
                _logger.LogWarning("GetOrganizationReportDataAsync called with empty OrganizationId");
                throw new BadRequestException("OrganizationId is required.");
            }

            if (reportId == Guid.Empty)
            {
                _logger.LogWarning("GetOrganizationReportDataAsync called with empty ReportId");
                throw new BadRequestException("ReportId is required.");
            }

            var reportDataResponse = await _organizationReportRepo.GetReportDataAsync(organizationId, reportId);

            if (reportDataResponse == null)
            {
                _logger.LogWarning("No report data found for organization {organizationId} and report {reportId}",
                    organizationId, reportId);
                throw new NotFoundException("Organization report data not found.");
            }

            _logger.LogInformation("Successfully retrieved organization report data for organization {organizationId} and report {reportId}",
                organizationId, reportId);

            return reportDataResponse;
        }
        catch (Exception ex) when (!(ex is BadRequestException || ex is NotFoundException))
        {
            _logger.LogError(ex, "Error fetching organization report data for organization {organizationId} and report {reportId}",
                organizationId, reportId);
            throw;
        }
    }
}

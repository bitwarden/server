using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportApplicationDataQuery : IGetOrganizationReportApplicationDataQuery
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<GetOrganizationReportApplicationDataQuery> _logger;

    public GetOrganizationReportApplicationDataQuery(
        IOrganizationReportRepository organizationReportRepo,
        ILogger<GetOrganizationReportApplicationDataQuery> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _logger = logger;
    }

    public async Task<OrganizationReportApplicationDataResponse> GetOrganizationReportApplicationDataAsync(Guid organizationId, Guid reportId)
    {
        try
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, "Fetching organization report application data for organization {organizationId} and report {reportId}",
                organizationId, reportId);

            if (organizationId == Guid.Empty)
            {
                _logger.LogWarning(Constants.BypassFiltersEventId, "GetOrganizationReportApplicationDataAsync called with empty OrganizationId");
                throw new BadRequestException("OrganizationId is required.");
            }

            if (reportId == Guid.Empty)
            {
                _logger.LogWarning(Constants.BypassFiltersEventId, "GetOrganizationReportApplicationDataAsync called with empty ReportId");
                throw new BadRequestException("ReportId is required.");
            }

            var applicationDataResponse = await _organizationReportRepo.GetApplicationDataAsync(organizationId, reportId);

            if (applicationDataResponse == null)
            {
                _logger.LogWarning(Constants.BypassFiltersEventId, "No application data found for organization {organizationId} and report {reportId}",
                    organizationId, reportId);
                throw new NotFoundException("Organization report application data not found.");
            }

            _logger.LogInformation(Constants.BypassFiltersEventId, "Successfully retrieved organization report application data for organization {organizationId} and report {reportId}",
                organizationId, reportId);

            return applicationDataResponse;
        }
        catch (Exception ex) when (!(ex is BadRequestException || ex is NotFoundException))
        {
            _logger.LogError(ex, "Error fetching organization report application data for organization {organizationId} and report {reportId}",
                organizationId, reportId);
            throw;
        }
    }
}

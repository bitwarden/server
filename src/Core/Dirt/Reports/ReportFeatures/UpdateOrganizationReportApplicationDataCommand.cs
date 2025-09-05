using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class UpdateOrganizationReportApplicationDataCommand : IUpdateOrganizationReportApplicationDataCommand
{
    private readonly IOrganizationRepository _organizationRepo;
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<UpdateOrganizationReportApplicationDataCommand> _logger;

    public UpdateOrganizationReportApplicationDataCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationReportRepository organizationReportRepository,
        ILogger<UpdateOrganizationReportApplicationDataCommand> logger)
    {
        _organizationRepo = organizationRepository;
        _organizationReportRepo = organizationReportRepository;
        _logger = logger;
    }

    public async Task<OrganizationReport> UpdateOrganizationReportApplicationDataAsync(UpdateOrganizationReportApplicationDataRequest request)
    {
        try
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, "Updating organization report application data {reportId} for organization {organizationId}",
                request.Id, request.OrganizationId);

            var (isValid, errorMessage) = await ValidateRequestAsync(request);
            if (!isValid)
            {
                _logger.LogWarning(Constants.BypassFiltersEventId, "Failed to update organization report application data {reportId} for organization {organizationId}: {errorMessage}",
                    request.Id, request.OrganizationId, errorMessage);
                throw new BadRequestException(errorMessage);
            }

            var existingReport = await _organizationReportRepo.GetByIdAsync(request.Id);
            if (existingReport == null)
            {
                _logger.LogWarning(Constants.BypassFiltersEventId, "Organization report {reportId} not found", request.Id);
                throw new NotFoundException("Organization report not found");
            }

            if (existingReport.OrganizationId != request.OrganizationId)
            {
                _logger.LogWarning(Constants.BypassFiltersEventId, "Organization report {reportId} does not belong to organization {organizationId}",
                    request.Id, request.OrganizationId);
                throw new BadRequestException("Organization report does not belong to the specified organization");
            }

            var updatedReport = await _organizationReportRepo.UpdateApplicationDataAsync(request.OrganizationId, request.Id, request.ApplicationData);

            _logger.LogInformation(Constants.BypassFiltersEventId, "Successfully updated organization report application data {reportId} for organization {organizationId}",
                request.Id, request.OrganizationId);

            return updatedReport;
        }
        catch (Exception ex) when (!(ex is BadRequestException || ex is NotFoundException))
        {
            _logger.LogError(ex, "Error updating organization report application data {reportId} for organization {organizationId}",
                request.Id, request.OrganizationId);
            throw;
        }
    }

    private async Task<(bool isValid, string errorMessage)> ValidateRequestAsync(UpdateOrganizationReportApplicationDataRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return (false, "OrganizationId is required");
        }

        if (request.Id == Guid.Empty)
        {
            return (false, "Id is required");
        }

        var organization = await _organizationRepo.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            return (false, "Invalid Organization");
        }

        if (string.IsNullOrWhiteSpace(request.ApplicationData))
        {
            return (false, "Application Data is required");
        }

        return (true, string.Empty);
    }
}

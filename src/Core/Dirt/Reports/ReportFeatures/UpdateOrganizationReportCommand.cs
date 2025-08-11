using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class UpdateOrganizationReportCommand : IUpdateOrganizationReportCommand
{
    private readonly IOrganizationRepository _organizationRepo;
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<UpdateOrganizationReportCommand> _logger;

    public UpdateOrganizationReportCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationReportRepository organizationReportRepository,
        ILogger<UpdateOrganizationReportCommand> logger)
    {
        _organizationRepo = organizationRepository;
        _organizationReportRepo = organizationReportRepository;
        _logger = logger;
    }

    public async Task<OrganizationReport> UpdateOrganizationReportAsync(UpdateOrganizationReportRequest request)
    {
        try
        {
            _logger.LogInformation("Updating organization report {reportId} for organization {organizationId}",
                request.ReportId, request.OrganizationId);

            var (isValid, errorMessage) = await ValidateRequestAsync(request);
            if (!isValid)
            {
                _logger.LogWarning("Failed to update organization report {reportId} for organization {organizationId}: {errorMessage}",
                    request.ReportId, request.OrganizationId, errorMessage);
                throw new BadRequestException(errorMessage);
            }

            var existingReport = await _organizationReportRepo.GetByIdAsync(request.ReportId);
            if (existingReport == null)
            {
                _logger.LogWarning("Organization report {reportId} not found", request.ReportId);
                throw new NotFoundException("Organization report not found");
            }

            if (existingReport.OrganizationId != request.OrganizationId)
            {
                _logger.LogWarning("Organization report {reportId} does not belong to organization {organizationId}",
                    request.ReportId, request.OrganizationId);
                throw new BadRequestException("Organization report does not belong to the specified organization");
            }

            existingReport.ReportData = request.ReportData;
            existingReport.RevisionDate = DateTime.UtcNow;

            var updatedReport = await _organizationReportRepo.ReplaceAsync(existingReport);

            _logger.LogInformation("Successfully updated organization report {reportId} for organization {organizationId}",
                request.ReportId, request.OrganizationId);

            return updatedReport;
        }
        catch (Exception ex) when (!(ex is BadRequestException || ex is NotFoundException))
        {
            _logger.LogError(ex, "Error updating organization report {reportId} for organization {organizationId}",
                request.ReportId, request.OrganizationId);
            throw;
        }
    }

    private async Task<(bool IsValid, string errorMessage)> ValidateRequestAsync(UpdateOrganizationReportRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return (false, "OrganizationId is required");
        }

        if (request.ReportId == Guid.Empty)
        {
            return (false, "ReportId is required");
        }

        var organization = await _organizationRepo.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            return (false, "Invalid Organization");
        }

        if (string.IsNullOrWhiteSpace(request.ReportData))
        {
            return (false, "Report Data is required");
        }

        return (true, string.Empty);
    }
}

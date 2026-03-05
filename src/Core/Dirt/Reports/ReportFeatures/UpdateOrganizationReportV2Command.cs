using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class UpdateOrganizationReportV2Command : IUpdateOrganizationReportV2Command
{
    private readonly IOrganizationRepository _organizationRepo;
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<UpdateOrganizationReportV2Command> _logger;

    public UpdateOrganizationReportV2Command(
        IOrganizationRepository organizationRepository,
        IOrganizationReportRepository organizationReportRepository,
        ILogger<UpdateOrganizationReportV2Command> logger)
    {
        _organizationRepo = organizationRepository;
        _organizationReportRepo = organizationReportRepository;
        _logger = logger;
    }

    public async Task<OrganizationReport> UpdateAsync(UpdateOrganizationReportV2Request request)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Updating v2 organization report {reportId} for organization {organizationId}",
            request.ReportId, request.OrganizationId);

        var (isValid, errorMessage) = await ValidateRequestAsync(request);
        if (!isValid)
        {
            _logger.LogWarning(Constants.BypassFiltersEventId,
                "Failed to update v2 organization report {reportId} for organization {organizationId}: {errorMessage}",
                request.ReportId, request.OrganizationId, errorMessage);
            throw new BadRequestException(errorMessage);
        }

        var existingReport = await _organizationReportRepo.GetByIdAsync(request.ReportId);
        if (existingReport == null)
        {
            _logger.LogWarning(Constants.BypassFiltersEventId,
                "Organization report {reportId} not found", request.ReportId);
            throw new NotFoundException("Organization report not found");
        }

        if (existingReport.OrganizationId != request.OrganizationId)
        {
            _logger.LogWarning(Constants.BypassFiltersEventId,
                "Organization report {reportId} does not belong to organization {organizationId}",
                request.ReportId, request.OrganizationId);
            throw new BadRequestException("Organization report does not belong to the specified organization");
        }

        if (request.ContentEncryptionKey != null)
        {
            existingReport.ContentEncryptionKey = request.ContentEncryptionKey;
        }

        if (request.SummaryData != null)
        {
            existingReport.SummaryData = request.SummaryData;
        }

        if (request.ApplicationData != null)
        {
            existingReport.ApplicationData = request.ApplicationData;
        }

        if (request.ReportMetrics != null)
        {
            existingReport.ApplicationCount = request.ReportMetrics.ApplicationCount;
            existingReport.ApplicationAtRiskCount = request.ReportMetrics.ApplicationAtRiskCount;
            existingReport.CriticalApplicationCount = request.ReportMetrics.CriticalApplicationCount;
            existingReport.CriticalApplicationAtRiskCount = request.ReportMetrics.CriticalApplicationAtRiskCount;
            existingReport.MemberCount = request.ReportMetrics.MemberCount;
            existingReport.MemberAtRiskCount = request.ReportMetrics.MemberAtRiskCount;
            existingReport.CriticalMemberCount = request.ReportMetrics.CriticalMemberCount;
            existingReport.CriticalMemberAtRiskCount = request.ReportMetrics.CriticalMemberAtRiskCount;
            existingReport.PasswordCount = request.ReportMetrics.PasswordCount;
            existingReport.PasswordAtRiskCount = request.ReportMetrics.PasswordAtRiskCount;
            existingReport.CriticalPasswordCount = request.ReportMetrics.CriticalPasswordCount;
            existingReport.CriticalPasswordAtRiskCount = request.ReportMetrics.CriticalPasswordAtRiskCount;
        }

        if (request.RequiresNewFileUpload)
        {
            var fileData = new ReportFile
            {
                Id = CoreHelpers.SecureRandomString(32, upper: false, special: false),
                FileName = "report-data.json",
                Validated = false,
                Size = 0
            };
            existingReport.SetReportFile(fileData);
        }

        existingReport.RevisionDate = DateTime.UtcNow;
        await _organizationReportRepo.ReplaceAsync(existingReport);

        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Successfully updated v2 organization report {reportId} for organization {organizationId}",
            request.ReportId, request.OrganizationId);

        return existingReport;
    }

    private async Task<(bool IsValid, string errorMessage)> ValidateRequestAsync(
        UpdateOrganizationReportV2Request request)
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

        return (true, string.Empty);
    }
}

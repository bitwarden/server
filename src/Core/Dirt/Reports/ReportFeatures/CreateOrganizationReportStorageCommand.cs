using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class CreateOrganizationReportStorageCommand : ICreateOrganizationReportStorageCommand
{
    private readonly IOrganizationRepository _organizationRepo;
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<CreateOrganizationReportStorageCommand> _logger;

    public CreateOrganizationReportStorageCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationReportRepository organizationReportRepository,
        ILogger<CreateOrganizationReportStorageCommand> logger)
    {
        _organizationRepo = organizationRepository;
        _organizationReportRepo = organizationReportRepository;
        _logger = logger;
    }

    public async Task<OrganizationReport> CreateAsync(AddOrganizationReportRequest request)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Creating organization report for organization {organizationId}", request.OrganizationId);

        var (isValid, errorMessage) = await ValidateRequestAsync(request);
        if (!isValid)
        {
            _logger.LogInformation(Constants.BypassFiltersEventId,
                "Failed to create organization {organizationId} report: {errorMessage}",
                request.OrganizationId, errorMessage);
            throw new BadRequestException(errorMessage);
        }

        var reportFileId = CoreHelpers.SecureRandomString(32, upper: false, special: false);

        var organizationReport = new OrganizationReport
        {
            OrganizationId = request.OrganizationId,
            ReportData = string.Empty,
            CreationDate = DateTime.UtcNow,
            ContentEncryptionKey = request.ContentEncryptionKey ?? string.Empty,
            SummaryData = request.SummaryData,
            ApplicationData = request.ApplicationData,
            FileId = reportFileId,
            ApplicationCount = request.ReportMetrics?.ApplicationCount,
            ApplicationAtRiskCount = request.ReportMetrics?.ApplicationAtRiskCount,
            CriticalApplicationCount = request.ReportMetrics?.CriticalApplicationCount,
            CriticalApplicationAtRiskCount = request.ReportMetrics?.CriticalApplicationAtRiskCount,
            MemberCount = request.ReportMetrics?.MemberCount,
            MemberAtRiskCount = request.ReportMetrics?.MemberAtRiskCount,
            CriticalMemberCount = request.ReportMetrics?.CriticalMemberCount,
            CriticalMemberAtRiskCount = request.ReportMetrics?.CriticalMemberAtRiskCount,
            PasswordCount = request.ReportMetrics?.PasswordCount,
            PasswordAtRiskCount = request.ReportMetrics?.PasswordAtRiskCount,
            CriticalPasswordCount = request.ReportMetrics?.CriticalPasswordCount,
            CriticalPasswordAtRiskCount = request.ReportMetrics?.CriticalPasswordAtRiskCount,
            RevisionDate = DateTime.UtcNow
        };

        var data = await _organizationReportRepo.CreateAsync(organizationReport);

        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Successfully created organization report for organization {organizationId}, {organizationReportId}",
            request.OrganizationId, data.Id);

        return data;
    }

    private async Task<(bool IsValid, string errorMessage)> ValidateRequestAsync(
        AddOrganizationReportRequest request)
    {
        var organization = await _organizationRepo.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            return (false, "Invalid Organization");
        }

        if (string.IsNullOrWhiteSpace(request.ContentEncryptionKey))
        {
            return (false, "Content Encryption Key is required");
        }

        return (true, string.Empty);
    }
}

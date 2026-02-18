using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class CreateOrganizationReportFileStorageCommand : ICreateOrganizationReportFileStorageCommand
{
    private readonly IOrganizationRepository _organizationRepo;
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<CreateOrganizationReportFileStorageCommand> _logger;

    public CreateOrganizationReportFileStorageCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationReportRepository organizationReportRepository,
        ILogger<CreateOrganizationReportFileStorageCommand> logger)
    {
        _organizationRepo = organizationRepository;
        _organizationReportRepo = organizationReportRepository;
        _logger = logger;
    }

    public async Task<(OrganizationReport Report, string ReportFileId)> CreateAsync(AddOrganizationReportRequest request)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Creating file-storage organization report for organization {organizationId}", request.OrganizationId);

        var (isValid, errorMessage) = await ValidateRequestAsync(request);
        if (!isValid)
        {
            _logger.LogInformation(Constants.BypassFiltersEventId,
                "Failed to create organization {organizationId} report: {errorMessage}",
                request.OrganizationId, errorMessage);
            throw new BadRequestException(errorMessage);
        }

        // Generate plaintext FileId for blob storage operations
        var reportFileId = CoreHelpers.SecureRandomString(32, upper: false, special: false);

        var requestMetrics = request.Metrics ?? new OrganizationReportMetricsRequest();

        var organizationReport = new OrganizationReport
        {
            OrganizationId = request.OrganizationId,
            ReportData = string.Empty, // Data goes to blob storage, not DB
            CreationDate = DateTime.UtcNow,
            ContentEncryptionKey = request.ContentEncryptionKey ?? string.Empty,
            SummaryData = null, // Data goes to blob storage, not DB
            ApplicationData = null, // Data goes to blob storage, not DB
            FileId = request.FileId, // Client-encrypted FileId (if provided)
            ApplicationCount = requestMetrics.ApplicationCount,
            ApplicationAtRiskCount = requestMetrics.ApplicationAtRiskCount,
            CriticalApplicationCount = requestMetrics.CriticalApplicationCount,
            CriticalApplicationAtRiskCount = requestMetrics.CriticalApplicationAtRiskCount,
            MemberCount = requestMetrics.MemberCount,
            MemberAtRiskCount = requestMetrics.MemberAtRiskCount,
            CriticalMemberCount = requestMetrics.CriticalMemberCount,
            CriticalMemberAtRiskCount = requestMetrics.CriticalMemberAtRiskCount,
            PasswordCount = requestMetrics.PasswordCount,
            PasswordAtRiskCount = requestMetrics.PasswordAtRiskCount,
            CriticalPasswordCount = requestMetrics.CriticalPasswordCount,
            CriticalPasswordAtRiskCount = requestMetrics.CriticalPasswordAtRiskCount,
            RevisionDate = DateTime.UtcNow
        };

        organizationReport.SetNewId();

        var data = await _organizationReportRepo.CreateAsync(organizationReport);

        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Successfully created file-storage organization report for organization {organizationId}, {organizationReportId}",
            request.OrganizationId, data.Id);

        return (data, reportFileId);
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

        // For file storage, data fields are NOT required in the request
        // They will be uploaded separately to blob storage

        return (true, string.Empty);
    }
}

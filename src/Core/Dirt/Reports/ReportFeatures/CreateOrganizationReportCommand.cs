using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class CreateOrganizationReportCommand : ICreateOrganizationReportCommand
{
    private readonly IOrganizationRepository _organizationRepo;
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<CreateOrganizationReportCommand> _logger;
    private readonly IFusionCache _cache;

    public CreateOrganizationReportCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationReportRepository organizationReportRepository,
        ILogger<CreateOrganizationReportCommand> logger,
        [FromKeyedServices(OrganizationReportCacheConstants.CacheName)] IFusionCache cache)
    {
        _organizationRepo = organizationRepository;
        _organizationReportRepo = organizationReportRepository;
        _logger = logger;
        _cache = cache;
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

        var fileData = new ReportFile
        {
            Id = CoreHelpers.SecureRandomString(32, upper: false, special: false),
            FileName = "report-data.json",
            Size = request.FileSize ?? 0,
            Validated = false
        };

        var organizationReport = new OrganizationReport
        {
            OrganizationId = request.OrganizationId,
            CreationDate = DateTime.UtcNow,
            ContentEncryptionKey = request.ContentEncryptionKey ?? string.Empty,
            SummaryData = request.SummaryData,
            ApplicationData = request.ApplicationData,
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
        organizationReport.SetReportFile(fileData);

        var data = await _organizationReportRepo.CreateAsync(organizationReport);

        await _cache.RemoveByTagAsync(OrganizationReportCacheConstants.BuildCacheTagForOrganizationReports(request.OrganizationId));

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

        if (string.IsNullOrWhiteSpace(request.SummaryData))
        {
            return (false, "Summary Data is required");
        }

        if (string.IsNullOrWhiteSpace(request.ApplicationData))
        {
            return (false, "Application Data is required");
        }

        if (request.ReportMetrics == null)
        {
            return (false, "Report Metrics is required");
        }

        return (true, string.Empty);
    }
}

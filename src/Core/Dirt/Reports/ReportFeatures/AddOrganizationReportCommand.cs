using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class AddOrganizationReportCommand : IAddOrganizationReportCommand
{
    private readonly IOrganizationRepository _organizationRepo;
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private ILogger<AddOrganizationReportCommand> _logger;

    public AddOrganizationReportCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationReportRepository organizationReportRepository,
        ILogger<AddOrganizationReportCommand> logger)
    {
        _organizationRepo = organizationRepository;
        _organizationReportRepo = organizationReportRepository;
        _logger = logger;
    }

    public async Task<OrganizationReport> AddOrganizationReportAsync(AddOrganizationReportRequest request)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "Adding organization report for organization {organizationId}", request.OrganizationId);

        var (isValid, errorMessage) = await ValidateRequestAsync(request);
        if (!isValid)
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, "Failed to add organization {organizationId} report: {errorMessage}", request.OrganizationId, errorMessage);
            throw new BadRequestException(errorMessage);
        }

        var requestMetrics = request.Metrics ?? new OrganizationReportMetricsRequest();

        var organizationReport = new OrganizationReport
        {
            OrganizationId = request.OrganizationId,
            ReportData = request.ReportData ?? string.Empty,
            CreationDate = DateTime.UtcNow,
            ContentEncryptionKey = request.ContentEncryptionKey ?? string.Empty,
            SummaryData = request.SummaryData,
            ApplicationData = request.ApplicationData,
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

        _logger.LogInformation(Constants.BypassFiltersEventId, "Successfully added organization report for organization {organizationId}, {organizationReportId}",
                request.OrganizationId, data.Id);

        return data;
    }

    private async Task<(bool IsValid, string errorMessage)> ValidateRequestAsync(
        AddOrganizationReportRequest request)
    {
        // verify that the organization exists
        var organization = await _organizationRepo.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            return (false, "Invalid Organization");
        }

        if (string.IsNullOrWhiteSpace(request.ContentEncryptionKey))
        {
            return (false, "Content Encryption Key is required");
        }

        if (string.IsNullOrWhiteSpace(request.ReportData))
        {
            return (false, "Report Data is required");
        }

        if (string.IsNullOrWhiteSpace(request.SummaryData))
        {
            return (false, "Summary Data is required");
        }

        if (string.IsNullOrWhiteSpace(request.ApplicationData))
        {
            return (false, "Application Data is required");
        }

        return (true, string.Empty);
    }
}

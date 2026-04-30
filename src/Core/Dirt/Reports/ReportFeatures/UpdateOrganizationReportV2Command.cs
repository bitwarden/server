using Bit.Core.Dirt.Entities;
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

public class UpdateOrganizationReportV2Command : IUpdateOrganizationReportV2Command
{
    private readonly IOrganizationRepository _organizationRepo;
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<UpdateOrganizationReportV2Command> _logger;
    private readonly IFusionCache _cache;

    public UpdateOrganizationReportV2Command(
        IOrganizationRepository organizationRepository,
        IOrganizationReportRepository organizationReportRepository,
        ILogger<UpdateOrganizationReportV2Command> logger,
        [FromKeyedServices(OrganizationReportCacheConstants.CacheName)] IFusionCache cache)
    {
        _organizationRepo = organizationRepository;
        _organizationReportRepo = organizationReportRepository;
        _logger = logger;
        _cache = cache;
    }

    public async Task<OrganizationReport> UpdateAsync(UpdateOrganizationReportV2Request request)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Updating v2 organization report {reportId} for organization {organizationId}",
            request.ReportId, request.OrganizationId);

        var (isValid, errorMessage) = await ValidateRequestAsync(request);
        if (!isValid)
        {
            throw new BadRequestException(errorMessage);
        }

        var existingReport = await _organizationReportRepo.GetByIdAsync(request.ReportId);
        if (existingReport == null)
        {
            throw new NotFoundException("Organization report not found");
        }

        if (existingReport.OrganizationId != request.OrganizationId)
        {
            throw new BadRequestException("Organization report does not belong to the specified organization");
        }

        existingReport.ContentEncryptionKey = request.ContentEncryptionKey;
        existingReport.SummaryData = request.SummaryData;
        existingReport.ApplicationData = request.ApplicationData;
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

        existingReport.RevisionDate = DateTime.UtcNow;
        await _organizationReportRepo.ReplaceAsync(existingReport);

        await _cache.RemoveByTagAsync(OrganizationReportCacheConstants.BuildCacheTagForOrganizationReports(request.OrganizationId));

        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Successfully updated v2 organization report {reportId} for organization {organizationId}",
            request.ReportId, request.OrganizationId);

        return existingReport;
    }

    private async Task<(bool IsValid, string errorMessage)> ValidateRequestAsync(
        UpdateOrganizationReportV2Request request)
    {
        var organization = await _organizationRepo.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            return (false, "Invalid Organization");
        }

        return (true, string.Empty);
    }
}

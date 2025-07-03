using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class DropOrganizationReportCommand : IDropOrganizationReportCommand
{
    private IOrganizationReportRepository _organizationReportRepo;
    private ILogger<DropOrganizationReportCommand> _logger;

    public DropOrganizationReportCommand(
        IOrganizationReportRepository organizationReportRepository,
        ILogger<DropOrganizationReportCommand> logger)
    {
        _organizationReportRepo = organizationReportRepository;
        _logger = logger;
    }

    public async Task DropOrganizationReportAsync(DropOrganizationReportRequest request)
    {
        _logger.LogInformation("Dropping organization report for organization {organizationId}",
            request.OrganizationId);

        var data = await _organizationReportRepo.GetByOrganizationIdAsync(request.OrganizationId);
        if (data == null || data.Count() == 0)
        {
            _logger.LogInformation("No organization reports found for organization {organizationId}", request.OrganizationId);
            throw new BadRequestException("No data found.");
        }

        data
            .Where(_ => request.OrganizationReportIds.Contains(_.Id))
            .ToList()
            .ForEach(async reportId =>
                {
                    _logger.LogInformation("Dropping organization report {organizationReportId} for organization {organizationId}",
                            reportId, request.OrganizationId);

                    await _organizationReportRepo.DeleteAsync(reportId);
                });
    }
}

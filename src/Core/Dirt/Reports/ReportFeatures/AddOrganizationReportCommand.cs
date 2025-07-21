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
        _logger.LogInformation("Adding organization report for organization {organizationId}", request.OrganizationId);

        var (isValid, errorMessage) = await ValidateRequestAsync(request);
        if (!isValid)
        {
            _logger.LogInformation("Failed to add organization {organizationId} report: {errorMessage}", request.OrganizationId, errorMessage);
            throw new BadRequestException(errorMessage);
        }

        var organizationReport = new OrganizationReport
        {
            OrganizationId = request.OrganizationId,
            ReportData = request.ReportData,
            Date = request.Date == default ? DateTime.UtcNow : request.Date,
            CreationDate = DateTime.UtcNow,
        };

        organizationReport.SetNewId();

        var data = await _organizationReportRepo.CreateAsync(organizationReport);

        _logger.LogInformation("Successfully added organization report for organization {organizationId}, {organizationReportId}",
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

        // ensure that we have report data
        if (string.IsNullOrWhiteSpace(request.ReportData))
        {
            return (false, "Report Data is required");
        }

        return (true, string.Empty);
    }
}

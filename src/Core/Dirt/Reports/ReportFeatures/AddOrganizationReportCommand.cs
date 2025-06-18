using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class AddOrganizationReportCommand : IAddOrganizationReportCommand
{
    private readonly IOrganizationRepository _organizationRepo;
    private readonly IOrganizationReportRepository _organizationReportRepo;

    public AddOrganizationReportCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationReportRepository organizationReportRepository)
    {
        _organizationRepo = organizationRepository;
        _organizationReportRepo = organizationReportRepository;
    }

    public async Task<OrganizationReport> AddOrganizationReportAsync(AddOrganizationReportRequest request)
    {
        var (req, IsValid, errorMessage) = await ValidateRequestAsync(request);
        if (!IsValid)
        {
            throw new BadRequestException(errorMessage);
        }

        var organizationReport = new OrganizationReport
        {
            OrganizationId = request.OrganizationId,
            ReportData = request.ReportData,
            Date = request.Date == default ? DateTime.UtcNow : request.Date,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };

        organizationReport.SetNewId();

        var data = await _organizationReportRepo.CreateAsync(organizationReport);
        return data;
    }

    private async Task<Tuple<AddOrganizationReportRequest, bool, string>> ValidateRequestAsync(
        AddOrganizationReportRequest request)
    {
        // verify that the organization exists
        var organization = await _organizationRepo.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            return new Tuple<AddOrganizationReportRequest, bool, string>(request, false, "Invalid Organization");
        }

        // ensure that we have a URL
        if (string.IsNullOrWhiteSpace(request.ReportData))
        {
            return new Tuple<AddOrganizationReportRequest, bool, string>(request, false, "Report Data is required");
        }

        return new Tuple<AddOrganizationReportRequest, bool, string>(request, true, string.Empty);
    }
}

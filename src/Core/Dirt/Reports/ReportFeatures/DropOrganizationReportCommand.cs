using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class DropOrganizationReportCommand : IDropOrganizationReportCommand
{
    private IOrganizationReportRepository _organizationReportRepo;

    public DropOrganizationReportCommand(
        IOrganizationReportRepository organizationReportRepository)
    {
        _organizationReportRepo = organizationReportRepository;
    }

    public async Task DropOrganizationReportAsync(DropOrganizationReportRequest request)
    {
        var data = await _organizationReportRepo.GetByOrganizationIdAsync(request.OrganizationId);
        if (data == null || data.Count() == 0)
        {
            throw new BadRequestException("Organization does not have any records.");
        }

        data.Where(_ => request.OrganizationReportIds.Contains(_.Id)).ToList().ForEach(async _ =>
        {
            await _organizationReportRepo.DeleteAsync(_);
        });
    }
}

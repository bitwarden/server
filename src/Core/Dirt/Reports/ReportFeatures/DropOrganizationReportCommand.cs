using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class DropOrganizationReportCommand : IDropOrganizationReportCommand
{
    private IOrganizationReportRepository _OrganizationReportRepo;

    public DropOrganizationReportCommand(
        IOrganizationReportRepository OrganizationReportRepository)
    {
        _OrganizationReportRepo = OrganizationReportRepository;
    }

    public async Task DropOrganizationReportAsync(DropOrganizationReportRequest request)
    {
        var data = await _OrganizationReportRepo.GetByOrganizationIdAsync(request.OrganizationId);
        if (data == null)
        {
            throw new BadRequestException("Organization does not have any records.");
        }

        data.Where(_ => request.OrganizationReportIds.Contains(_.Id)).ToList().ForEach(async _ =>
        {
            await _OrganizationReportRepo.DeleteAsync(_);
        });
    }
}

using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class DropPasswordHealthReportApplicationCommand : IDropPasswordHealthReportApplicationCommand
{
    private IPasswordHealthReportApplicationRepository _passwordHealthReportApplicationRepo;

    public DropPasswordHealthReportApplicationCommand(
        IPasswordHealthReportApplicationRepository passwordHealthReportApplicationRepository)
    {
        _passwordHealthReportApplicationRepo = passwordHealthReportApplicationRepository;
    }

    public async Task DropPasswordHealthReportApplicationAsync(DropPasswordHealthReportApplicationRequest request)
    {
        var data = await _passwordHealthReportApplicationRepo.GetByOrganizationIdAsync(request.OrganizationId);
        if (data == null)
        {
            throw new BadRequestException("Organization does not have any records.");
        }

        data.Where(_ => request.PasswordHealthReportApplicationIds.Contains(_.Id)).ToList().ForEach(async _ =>
        {
            await _passwordHealthReportApplicationRepo.DeleteAsync(_);
        });
    }
}

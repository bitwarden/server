using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetPasswordHealthReportApplicationQuery : IGetPasswordHealthReportApplicationQuery
{
    private IPasswordHealthReportApplicationRepository _passwordHealthReportApplicationRepo;

    public GetPasswordHealthReportApplicationQuery(
        IPasswordHealthReportApplicationRepository passwordHealthReportApplicationRepo)
    {
        _passwordHealthReportApplicationRepo = passwordHealthReportApplicationRepo;
    }

    public async Task<IEnumerable<PasswordHealthReportApplication>> GetPasswordHealthReportApplicationAsync(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("OrganizationId is required.");
        }

        return await _passwordHealthReportApplicationRepo.GetByOrganizationIdAsync(organizationId);
    }
}

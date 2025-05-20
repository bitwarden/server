using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReportFeatures.Interfaces;
using Bit.Core.Tools.Repositories;

namespace Bit.Core.Tools.ReportFeatures;

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

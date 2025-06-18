using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportQuery : IGetOrganizationReportQuery
{
    private IOrganizationReportRepository _organizationReportRepo;

    public GetOrganizationReportQuery(
        IOrganizationReportRepository organizationReportRepo)
    {
        _organizationReportRepo = organizationReportRepo;
    }

    public async Task<IEnumerable<OrganizationReport>> GetOrganizationReportAsync(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("OrganizationId is required.");
        }

        return await _organizationReportRepo.GetByOrganizationIdAsync(organizationId);
    }

    public async Task<OrganizationReport> GetLatestOrganizationReportAsync(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("OrganizationId is required.");
        }

        return await _organizationReportRepo.GetLatestByOrganizationIdAsync(organizationId);
    }
}

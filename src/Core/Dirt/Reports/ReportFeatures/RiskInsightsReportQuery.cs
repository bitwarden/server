using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.OrganizationReportMembers.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class RiskInsightsReportQuery : IRiskInsightsReportQuery
{
    private readonly IOrganizationMemberBaseDetailRepository _organizationMemberBaseDetailRepository;

    public RiskInsightsReportQuery(IOrganizationMemberBaseDetailRepository repository)
    {
        _organizationMemberBaseDetailRepository = repository;
    }

    public async Task<IEnumerable<RiskInsightsReportDetail>> GetRiskInsightsReportDetails(
        RiskInsightsReportRequest request)
    {
        var baseDetails =
            await _organizationMemberBaseDetailRepository.GetOrganizationMemberBaseDetailsByOrganizationId(
                request.OrganizationId);

        var insightsDetails = baseDetails
            .GroupBy(b => new { b.OrganizationUserId, b.UserName, b.Email, b.UsesKeyConnector })
            .Select(g => new RiskInsightsReportDetail
            {
                UserGuid = g.Key.OrganizationUserId,
                UserName = g.Key.UserName,
                Email = g.Key.Email,
                UsesKeyConnector = g.Key.UsesKeyConnector,
                CipherIds = g
                    .Select(x => x.CipherId.ToString())
                    .Distinct()
            });

        return insightsDetails;
    }
}

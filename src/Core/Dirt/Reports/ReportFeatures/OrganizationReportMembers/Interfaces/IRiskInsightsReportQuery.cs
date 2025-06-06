using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Tools.ReportFeatures.Requests;

namespace Bit.Core.Tools.ReportFeatures.OrganizationReportMembers.Interfaces;

public interface IRiskInsightsReportQuery
{
    Task<IEnumerable<RiskInsightsReportDetail>> GetRiskInsightsReportDetails(RiskInsightsReportRequest request);
}

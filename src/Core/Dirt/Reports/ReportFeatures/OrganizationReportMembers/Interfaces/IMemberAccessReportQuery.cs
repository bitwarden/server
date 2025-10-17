using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures.OrganizationReportMembers.Interfaces;

public interface IMemberAccessReportQuery
{
    Task<IEnumerable<MemberAccessReportDetail>> GetMemberAccessReportsAsync(MemberAccessReportRequest request);
}

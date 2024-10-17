using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.ReportFeatures.Requests;

namespace Bit.Core.Tools.ReportFeatures.OrganizationReportMembers.Interfaces;

public interface IMemberAccessCipherDetailsQuery
{
    Task<IEnumerable<MemberAccessCipherDetails>> GetMemberAccessCipherDetails(MemberAccessCipherDetailsRequest request);
}

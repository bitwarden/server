using Core.Tools.Models.Data;
using Core.Tools.ReportFeatures.Requests;

namespace Core.Tools.ReportFeatures.OrganizationReportMembers.Interfaces;

public interface IMemberAccessCipherDetailsQuery
{
    Task<IEnumerable<MemberAccessCipherDetails>> GetMemberAccessCipherDetails(MemberAccessCipherDetailsRequest request);
}

using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Core.Tools.ReportFeatures.OrganizationReportMembers.Interfaces;
using Bit.Core.Tools.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

/// <summary>
/// Generates the member access report.
/// </summary>
public class MemberAccessCipherDetailsQuery : IMemberAccessCipherDetailsQuery
{
    private readonly IMemberAccessCipherDetailsRepository _memberAccessCipherDetailsRepository;

    public MemberAccessCipherDetailsQuery(
        IMemberAccessCipherDetailsRepository memberAccessCipherDetailsRepository
    )
    {
        _memberAccessCipherDetailsRepository = memberAccessCipherDetailsRepository;
    }

    /// <summary>
    /// Generates a report for all members of an organization. Containing summary information
    /// such as item, collection, and group counts. Including the cipherIds a member is assigned.
    /// Child collection includes detailed information on the  user and group collections along
    /// with their permissions.
    /// </summary>
    /// <param name="request"><see cref="MemberAccessCipherDetailsRequest"/>need organizationId field to get data</param>
    /// <returns>List of the <see cref="MemberAccessCipherDetails"/></returns>;
    public async Task<IEnumerable<MemberAccessCipherDetails>> GetMemberAccessCipherDetails(MemberAccessCipherDetailsRequest request)
    {
        return await _memberAccessCipherDetailsRepository.GetMemberAccessCipherDetailsByOrganizationId(request.OrganizationId);
    }
}

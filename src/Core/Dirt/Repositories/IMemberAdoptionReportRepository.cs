using Bit.Core.Dirt.Reports.Models.Data;

namespace Bit.Core.Dirt.Reports.Repositories;

public interface IMemberAdoptionReportRepository
{
    Task<IReadOnlyList<MemberAdoptionReportDetail>> GetMemberAdoptionDetailsByOrganizationIdAsync(Guid organizationId);
}

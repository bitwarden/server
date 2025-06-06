using Bit.Core.Dirt.Reports.Models.Data;

namespace Bit.Core.Dirt.Reports.Repositories;

public interface IMemberAccessCipherDetailsRepository
{
    Task<IEnumerable<MemberAccessCipherDetails>> GetMemberAccessCipherDetailsByOrganizationId(Guid organizationId);
}

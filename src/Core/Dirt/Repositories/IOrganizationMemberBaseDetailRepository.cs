using Bit.Core.Dirt.Reports.Models.Data;

namespace Bit.Core.Dirt.Reports.Repositories;

public interface IOrganizationMemberBaseDetailRepository
{
    Task<IEnumerable<OrganizationMemberBaseDetail>> GetOrganizationMemberBaseDetailsByOrganizationId(Guid organizationId);
}

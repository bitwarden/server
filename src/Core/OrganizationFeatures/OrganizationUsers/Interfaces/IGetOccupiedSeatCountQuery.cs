using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IGetOccupiedSeatCountQuery
{
    Task<int> GetOccupiedSeatCountAsync(Organization organization);
}

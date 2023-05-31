using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;

namespace Bit.Core.Repositories;

public interface IAuthRequestRepository : IRepository<AuthRequest, Guid>
{
    Task<int> DeleteExpiredAsync();
    Task<ICollection<AuthRequest>> GetManyByUserIdAsync(Guid userId);
    Task<ICollection<OrganizationAdminAuthRequest>> GetManyPendingByOrganizationIdAsync(Guid organizationId);
}

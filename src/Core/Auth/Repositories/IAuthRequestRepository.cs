using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;

namespace Bit.Core.Repositories;

public interface IAuthRequestRepository : IRepository<AuthRequest, Guid>
{
    Task<int> DeleteExpiredAsync(TimeSpan userExpiration, TimeSpan adminExpiration, TimeSpan adminApprovalExpiration);
    Task<ICollection<AuthRequest>> GetManyByUserIdAsync(Guid userId);
    Task<ICollection<OrganizationAdminAuthRequest>> GetManyPendingByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<OrganizationAdminAuthRequest>> GetManyAdminApprovalRequestsByManyIdsAsync(Guid organizationId, IEnumerable<Guid> ids);
}

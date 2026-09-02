using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;

#nullable enable

namespace Bit.Core.Repositories;

public interface IAuthRequestRepository : IRepository<AuthRequest, Guid>
{
    Task<int> DeleteExpiredAsync(TimeSpan userRequestExpiration, TimeSpan adminRequestExpiration, TimeSpan afterAdminApprovalExpiration);
    Task<ICollection<AuthRequest>> GetManyByUserIdAsync(Guid userId);
    /// <summary>
    /// Gets all active pending auth requests for a user. Each auth request in the collection will be associated with a different
    /// device. It will be the most current request for the device.
    /// </summary>
    /// <param name="userId">UserId of the owner of the AuthRequests</param>
    /// <returns>a collection Auth request details or empty</returns>
    Task<IEnumerable<PendingAuthRequestDetails>> GetManyPendingAuthRequestByUserId(Guid userId);
    Task<ICollection<OrganizationAdminAuthRequest>> GetManyPendingByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<OrganizationAdminAuthRequest>> GetManyAdminApprovalRequestsByManyIdsAsync(Guid organizationId, IEnumerable<Guid> ids);
    Task UpdateManyAsync(IEnumerable<AuthRequest> authRequests);
}

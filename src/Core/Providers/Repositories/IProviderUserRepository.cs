using Bit.Core.Providers.Entities;
using Bit.Core.Providers.Enums;
using Bit.Core.Providers.Models.Data;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Providers.Repositories;

public interface IProviderUserRepository : IRepository<ProviderUser, Guid>
{
    Task<int> GetCountByProviderAsync(Guid providerId, string email, bool onlyRegisteredUsers);
    Task<ICollection<ProviderUser>> GetManyAsync(IEnumerable<Guid> ids);
    Task<ICollection<ProviderUser>> GetManyByUserAsync(Guid userId);
    Task<ICollection<ProviderUser>> GetManyByManyUsersAsync(IEnumerable<Guid> userIds);
    Task<ProviderUser?> GetByProviderUserAsync(Guid providerId, Guid userId);
    Task<ICollection<ProviderUser>> GetManyByProviderAsync(Guid providerId, ProviderUserType? type = null);
    Task<ICollection<ProviderUserUserDetails>> GetManyDetailsByProviderAsync(Guid providerId, ProviderUserStatusType? status = null);
    Task<ICollection<ProviderUserProviderDetails>> GetManyDetailsByUserAsync(Guid userId,
        ProviderUserStatusType? status = null);
    Task<IEnumerable<ProviderUserOrganizationDetails>> GetManyOrganizationDetailsByUserAsync(Guid userId, ProviderUserStatusType? status = null);
    Task DeleteManyAsync(IEnumerable<Guid> userIds);
    Task<IEnumerable<ProviderUserPublicKey>> GetManyPublicKeysByProviderUserAsync(Guid providerId, IEnumerable<Guid> Ids);
    Task<int> GetCountByOnlyOwnerAsync(Guid userId);
    Task<ICollection<ProviderUser>> GetManyByOrganizationAsync(Guid organizationId, ProviderUserStatusType? status = null);
}

using Bit.Core.Entities.Provider;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories;

public interface IProviderUserRepository : IRepository<ProviderUser, Guid>
{
    Task<int> GetCountByProviderAsync(Guid providerId, string email, bool onlyRegisteredUsers);
    Task<ICollection<ProviderUser>> GetManyAsync(IEnumerable<Guid> ids);
    Task<ICollection<ProviderUser>> GetManyByUserAsync(Guid userId);
    Task<ProviderUser> GetByProviderUserAsync(Guid providerId, Guid userId);
    Task<ICollection<ProviderUser>> GetManyByProviderAsync(Guid providerId, ProviderUserType? type = null);
    Task<ICollection<ProviderUserUserDetails>> GetManyDetailsByProviderAsync(Guid providerId);
    Task<ICollection<ProviderUserProviderDetails>> GetManyDetailsByUserAsync(Guid userId,
        ProviderUserStatusType? status = null);
    Task<IEnumerable<ProviderUserOrganizationDetails>> GetManyOrganizationDetailsByUserAsync(Guid userId, ProviderUserStatusType? status = null);
    Task DeleteManyAsync(IEnumerable<Guid> userIds);
    Task<IEnumerable<ProviderUserPublicKey>> GetManyPublicKeysByProviderUserAsync(Guid providerId, IEnumerable<Guid> Ids);
    Task<int> GetCountByOnlyOwnerAsync(Guid userId);
}

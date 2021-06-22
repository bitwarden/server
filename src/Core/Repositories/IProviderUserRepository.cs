using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table.Provider;

namespace Bit.Core.Repositories
{
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
        Task DeleteManyAsync(IEnumerable<Guid> userIds);
        Task<IEnumerable<ProviderUserPublicKey>> GetManyPublicKeysByProviderUserAsync(Guid providerId, IEnumerable<Guid> Ids);
    }
}

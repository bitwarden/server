using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Enums.Provider;
using Bit.Core.Models.Table.Provider;

namespace Bit.Core.Repositories
{
    public interface IProviderUserRepository : IRepository<ProviderUser, Guid>
    {
        Task<int> GetCountByProviderAsync(Guid providerId, string email, bool onlyRegisteredUsers);
        Task<ICollection<ProviderUser>> GetManyAsync(IEnumerable<Guid> ids);
        Task<ICollection<ProviderUser>> GetManyByProviderAsync(Guid providerId, ProviderUserType? type = null);
<<<<<<< HEAD
=======
        Task<ICollection<ProviderUserUserDetails>> GetManyDetailsByProviderAsync(Guid providerId);
        Task<ICollection<ProviderUserProviderDetails>> GetManyDetailsByUserAsync(Guid userId,
            ProviderUserStatusType? status = null);
        Task<IEnumerable<ProviderUserOrganizationDetails>> GetManyOrganizationDetailsByUserAsync(Guid userId, ProviderUserStatusType? status = null);
>>>>>>> 545d5f942b1a2d210c9488c669d700d01d2c1aeb
        Task DeleteManyAsync(IEnumerable<Guid> userIds);
    }
}

using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories
{
    public interface ICollectionUserRepository : IRepository<CollectionUser, Guid>
    {
        Task<ICollection<CollectionUser>> GetManyByOrganizationUserIdAsync(Guid orgUserId);
        Task<ICollection<CollectionUserCollectionDetails>> GetManyDetailsByUserIdAsync(Guid userId);
        Task<ICollection<CollectionUserUserDetails>> GetManyDetailsByCollectionIdAsync(Guid collectionId);
        Task<bool> GetCanEditByUserIdCipherIdAsync(Guid userId, Guid cipherId);
    }
}

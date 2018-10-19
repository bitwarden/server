using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Core.Models.Data;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories
{
    public interface ICipherRepository : IRepository<Cipher, Guid>
    {
        Task<CipherDetails> GetByIdAsync(Guid id, Guid userId);
        Task<CipherDetails> GetDetailsByIdAsync(Guid id);
        Task<bool> GetCanEditByIdAsync(Guid userId, Guid cipherId);
        Task<ICollection<CipherDetails>> GetManyByUserIdAsync(Guid userId, bool withOrganizations = true);
        Task<ICollection<Cipher>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task CreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds);
        Task CreateAsync(CipherDetails cipher);
        Task CreateAsync(CipherDetails cipher, IEnumerable<Guid> collectionIds);
        Task ReplaceAsync(CipherDetails cipher);
        Task UpsertAsync(CipherDetails cipher);
        Task<bool> ReplaceAsync(Cipher obj, IEnumerable<Guid> collectionIds);
        Task UpdatePartialAsync(Guid id, Guid userId, Guid? folderId, bool favorite);
        Task UpdateAttachmentAsync(CipherAttachment attachment);
        Task DeleteAttachmentAsync(Guid cipherId, string attachmentId);
        Task DeleteAsync(IEnumerable<Guid> ids, Guid userId);
        Task MoveAsync(IEnumerable<Guid> ids, Guid? folderId, Guid userId);
        Task DeleteByUserIdAsync(Guid userId);
        Task DeleteByOrganizationIdAsync(Guid organizationId);
        Task UpdateUserKeysAndCiphersAsync(User user, IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders);
        Task UpdateCiphersAsync(Guid userId, IEnumerable<Cipher> ciphers);
        Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Folder> folders);
        Task CreateAsync(IEnumerable<Cipher> ciphers, IEnumerable<Collection> collections,
            IEnumerable<CollectionCipher> collectionCiphers);
    }
}

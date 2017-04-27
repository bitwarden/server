using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Core.Models.Data;
using System;

namespace Bit.Core.Services
{
    public interface ICipherService
    {
        Task SaveAsync(Cipher cipher, Guid savingUserId, bool orgAdmin = false);
        Task SaveDetailsAsync(CipherDetails cipher, Guid savingUserId);
        Task DeleteAsync(Cipher cipher, Guid deletingUserId, bool orgAdmin = false);
        Task SaveFolderAsync(Folder folder);
        Task DeleteFolderAsync(Folder folder);
        Task ShareAsync(Cipher cipher, Guid organizationId, IEnumerable<Guid> collectionIds, Guid userId);
        Task SaveCollectionsAsync(Cipher cipher, IEnumerable<Guid> collectionIds, Guid savingUserId, bool orgAdmin);
        Task ImportCiphersAsync(List<Folder> folders, List<CipherDetails> ciphers,
            IEnumerable<KeyValuePair<int, int>> folderRelationships);
    }
}

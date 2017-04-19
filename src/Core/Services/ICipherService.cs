using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Core.Models.Data;
using System;

namespace Bit.Core.Services
{
    public interface ICipherService
    {
        Task SaveAsync(CipherDetails cipher, Guid savingUserId);
        Task DeleteAsync(Cipher cipher, Guid deletingUserId, bool orgAdmin = false);
        Task SaveFolderAsync(Folder folder);
        Task DeleteFolderAsync(Folder folder);
        Task ShareAsync(Cipher cipher, Guid organizationId, IEnumerable<Guid> subvaultIds, Guid userId);
        Task SaveSubvaultsAsync(Cipher cipher, IEnumerable<Guid> subvaultIds, Guid savingUserId, bool orgAdmin);
        Task ImportCiphersAsync(List<Folder> folders, List<CipherDetails> ciphers,
            IEnumerable<KeyValuePair<int, int>> folderRelationships);
    }
}

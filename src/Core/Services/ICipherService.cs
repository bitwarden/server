using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Core.Models.Data;

namespace Bit.Core.Services
{
    public interface ICipherService
    {
        Task SaveAsync(CipherDetails cipher);
        Task DeleteAsync(Cipher cipher);
        Task SaveFolderAsync(Folder folder);
        Task DeleteFolderAsync(Folder folder);
        Task ImportCiphersAsync(List<Folder> folders, List<CipherDetails> ciphers,
            IEnumerable<KeyValuePair<int, int>> folderRelationships);
    }
}

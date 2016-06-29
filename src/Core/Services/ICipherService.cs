using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Services
{
    public interface ICipherService
    {
        Task SaveAsync(Cipher cipher);
        Task DeleteAsync(Cipher cipher);
        Task ImportCiphersAsync(List<Cipher> folders, List<Cipher> ciphers, IEnumerable<KeyValuePair<int, int>> folderRelationships);
    }
}

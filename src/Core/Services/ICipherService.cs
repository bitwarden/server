using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Services
{
    public interface ICipherService
    {
        Task ImportCiphersAsync(List<Cipher> folders, List<Cipher> ciphers, IEnumerable<KeyValuePair<int, int>> folderRelationships);
    }
}

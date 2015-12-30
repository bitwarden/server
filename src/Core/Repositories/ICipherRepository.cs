using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bit.Core.Repositories
{
    public interface ICipherRepository
    {
        Task DirtyCiphersAsync(string userId);
        Task UpdateDirtyCiphersAsync(IEnumerable<dynamic> ciphers);
        Task CreateAsync(IEnumerable<dynamic> ciphers);
    }
}

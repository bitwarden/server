using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Repositories
{
    public interface ICipherRepository
    {
        Task UpdateUserEmailPasswordAndCiphersAsync(User user, IEnumerable<dynamic> ciphers);
        Task CreateAsync(IEnumerable<dynamic> ciphers);
    }
}

using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Repositories
{
    public interface ISsoUserRepository
    {
        Task CreateAsync(SsoUser obj);
        Task DeleteAsync(SsoUser obj);
    }
}
